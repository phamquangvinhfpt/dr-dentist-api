using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
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

        await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant.Id}");

        var isOnline = await _presenceTracker.UserConnected(Context.User.Identity.Name, Context.ConnectionId);

        if (isOnline)
        {
            await Clients.Others.SendAsync("UserIsOnline", Context.User.Identity.Name);
        }

        var currentUsers = await _presenceTracker.GetOnlineUsers();
        await Clients.Caller.SendAsync("GetOnlineUsers", currentUsers);

        await base.OnConnectedAsync();

        _logger.LogInformation("A client connected to ChatHub: {connectionId}", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_currentTenant != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant.Id}");
        }

        var isOffline = await _presenceTracker.UserDisconnected(Context.User.Identity.Name, Context.ConnectionId);
        if (isOffline)
        {
            await Clients.Others.SendAsync("UserIsOffline", Context.User.Identity.Name);
        }

        await base.OnDisconnectedAsync(exception);

        _logger.LogInformation("A client disconnected from ChatHub: {connectionId}", Context.ConnectionId);
    }

    public async Task SendMessage(string? receiverId, string message, bool isStaffSender)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        var sentMessage = await _chatService.SendMessageAsync(receiverId, message, isStaffSender, default);
        await Clients.Group($"GroupTenant-{_currentTenant.Id}").SendAsync("ReceiveMessage", sentMessage);
    }

    public async Task GetConversation(string conversionId)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        var conversation = await _chatService.GetConversationAsync(conversionId, default);
        await Clients.Caller.SendAsync("ReceiveConversation", conversation);
    }

    public async Task JoinPatientGroup(string patientId)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetPatientGroupName(patientId));
        _logger.LogInformation("User {userId} joined patient group {patientId}", Context.UserIdentifier, patientId);
    }

    public async Task LeavePatientGroup(string patientId)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetPatientGroupName(patientId));
        _logger.LogInformation("User {userId} left patient group {patientId}", Context.UserIdentifier, patientId);
    }

    public async Task MarkMessagesAsRead(string patientId)
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await _chatService.MarkMessagesAsReadAsync(patientId, Context.UserIdentifier, default);
        _logger.LogInformation("Messages marked as read for patient {patientId} by user {userId}", patientId, Context.UserIdentifier);
    }

    private string GetPatientGroupName(string patientId) => $"Patient-{patientId}";
}