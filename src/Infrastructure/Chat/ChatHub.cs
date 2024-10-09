using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.CustomerServices;
using Microsoft.AspNetCore.Authorization;
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

    public ChatHub(ITenantInfo? currentTenant, ILogger<ChatHub> logger, IChatService chatService, PresenceTracker presenceTracker)
    {
        _currentTenant = currentTenant;
        _logger = logger;
        _chatService = chatService;
        _presenceTracker = presenceTracker;
    }

    public override async Task OnConnectedAsync()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        var name = Context.User.Claims.FirstOrDefault(c => c.Type.EndsWith("nameidentifier"))?.Value;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant.Id}");

        var (isOnline, onlineUsers) = await _presenceTracker.UserConnected(name, Context.ConnectionId);

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

        var sentMessage = await _chatService.SendMessageAsync(receiverId, message, default);
        await Clients.Group($"GroupTenant-{_currentTenant.Id}").SendAsync("ReceiveMessage", sentMessage);
    }

    public async Task<IEnumerable<ListMessageDto>> GetConversation(string conversionId)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        return await _chatService.GetConversationAsync(conversionId, default);
    }

    public async Task JoinPatientGroup()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetPatientGroupName());
        _logger.LogInformation("User {userId} joined patient group {patientId}", Context.UserIdentifier);
    }

    public async Task LeavePatientGroup()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPatientGroupName());
        _logger.LogInformation("User {userId} left patient group {patientId}", Context.UserIdentifier);
    }

    // public async Task MarkMessagesAsRead(string patientId)
    // {
    //     if (_currentTenant is null)
    //     {
    //         throw new UnauthorizedException("Authentication Failed.");
    //     }

    //     await _chatService.MarkMessagesAsReadAsync(patientId, Context.UserIdentifier, default);
    //     _logger.LogInformation("Messages marked as read for patient {patientId} by user {userId}", patientId, Context.UserIdentifier);
    // }

    private string GetPatientGroupName() => $"GroupTenant-{_currentTenant.Id}";
}