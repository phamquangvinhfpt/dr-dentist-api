using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Chat;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.Notifications;

[Authorize]
public class NotificationHub : Hub, ITransientService
{
    private readonly ITenantInfo? _currentTenant;
    private readonly ILogger<NotificationHub> _logger;
    private readonly IChatService _chatService;
    private readonly PresenceTracker _presenceTracker;
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationHub(ITenantInfo? currentTenant, ILogger<NotificationHub> logger, IChatService chatService,
        PresenceTracker presenceTracker, ICurrentUser currentUser, UserManager<ApplicationUser> userManager)
    {
        _currentTenant = currentTenant;
        _logger = logger;
        _chatService = chatService;
        _presenceTracker = presenceTracker;
        _currentUser = currentUser;
        _userManager = userManager;
    }

    public override async Task OnConnectedAsync()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant.Id}");

        var id = Context.User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier"))?.Value;

        var (isOnline, onlineUsers) = await _presenceTracker.UserConnected(id, Context.ConnectionId);

        var listStaff = await _userManager.GetUsersInRoleAsync(FSHRoles.Staff);

        if (isOnline)
        {
            await Clients.Others.SendAsync("UserIsOnline", onlineUsers);
        }

        await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);

        await base.OnConnectedAsync();

        _logger.LogInformation("A client connected to NotificationHub: {connectionId}", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant!.Id}");

        var name = Context.User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier"))?.Value;

        var (isOffline, onlineUsers) = await _presenceTracker.UserDisconnected(name, Context.ConnectionId);

        if (isOffline)
        {
            await Clients.Others.SendAsync("UserIsOffline", onlineUsers);
        }

        await Clients.All.SendAsync("UpdateOnlineUsers", onlineUsers);

        await base.OnDisconnectedAsync(exception);

        _logger.LogInformation("A client disconnected from NotificationHub: {connectionId}", Context.ConnectionId);
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