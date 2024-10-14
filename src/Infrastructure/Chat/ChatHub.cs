using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.Chat;

[Authorize]
public class ChatHub : Hub, ITransientService
{
    private readonly ITenantInfo? _currentTenant;
    private readonly ILogger<ChatHub> _logger;
    private readonly IChatService _chatService;
    private readonly PresenceTracker _presenceTracker;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public ChatHub(ITenantInfo? currentTenant, ILogger<ChatHub> logger, IChatService chatService, PresenceTracker presenceTracker, ICurrentUser currentUser, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager)
    {
        _currentTenant = currentTenant;
        _logger = logger;
        _chatService = chatService;
        _presenceTracker = presenceTracker;
        _currentUser = currentUser;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public override async Task OnConnectedAsync()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        var id = Context.User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier"))?.Value;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant.Id}");

        var (isOnline, onlineUsers) = await _presenceTracker.UserConnected(id, Context.ConnectionId);

        var listStaff = await _userManager.GetUsersInRoleAsync(FSHRoles.Staff);

        if (isOnline)
        {
            await Clients.Others.SendAsync("UserIsOnline", onlineUsers);
        }

        await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);

        await base.OnConnectedAsync();

        _logger.LogInformation("A client connected to ChatHub: {connectionId}", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        var name = Context.User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier"))?.Value;

        var (isOffline, onlineUsers) = await _presenceTracker.UserDisconnected(name, Context.ConnectionId);

        if (isOffline)
        {
            await Clients.Others.SendAsync("UserIsOffline", onlineUsers);
        }

        await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);

        await base.OnDisconnectedAsync(exception);

        _logger.LogInformation("A client disconnected from ChatHub: {connectionId}", Context.ConnectionId);
    }

    public async Task<List<ListUserDto>> GetListUserDto()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        return await _chatService.GetListUserDtoAsync();
    }

    public async Task SendMessage(string? receiverId, string message)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        _chatService.SetCurrentUser(Context.User);

        var listStaff = await _userManager.GetUsersInRoleAsync(FSHRoles.Staff);

        var sentMessage = await _chatService.SendMessageAsync(receiverId, message, default);
        if (_currentUser.IsInRole(FSHRoles.Staff))
        {
            await Clients.User(receiverId).SendAsync("ReceiveMessage", sentMessage);
            await Clients.Users(listStaff.Select(s => s.Id)).SendAsync("ReceiveMessage", sentMessage);
        }
        else
        {
            await Clients.Users(listStaff.Select(s => s.Id)).SendAsync("ReceiveMessage", sentMessage);
        }
    }

    public async Task<IEnumerable<ListMessageDto>> GetConversation(string conversionId)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        return await _chatService.GetConversationAsync(conversionId, default);
    }
}