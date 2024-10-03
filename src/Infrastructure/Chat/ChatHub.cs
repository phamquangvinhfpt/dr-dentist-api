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

    public ChatHub(ITenantInfo? currentTenant, ILogger<ChatHub> logger)
    {
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        if (_currentTenant is null)
        {
            throw new UnauthorizedException("Authentication Failed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant.Id}");

        await base.OnConnectedAsync();

        _logger.LogInformation("A client connected to ChatHub: {connectionId}", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"GroupTenant-{_currentTenant!.Id}");

        await base.OnDisconnectedAsync(exception);

        _logger.LogInformation("A client disconnected from ChatHub: {connectionId}", Context.ConnectionId);
    }
}
