using FSH.WebApi.Application.Chat;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Infrastructure.Chat;

namespace FSH.WebApi.Host.Controllers.Notification;

public class NotificationsController : VersionedApiController
{
    private readonly IChatService _chatService;

    public NotificationsController(IChatService chatService)
    {
        _chatService = chatService;
    }

    // API Just for testing purposes, it'll be removed in the future
    [HttpGet("test-send-notification")]
    [OpenApiOperation("Test notification", "")]
    public Task<string> TestNotification()
    {
        return Mediator.Send(new TestNotificationRequest());
    }

    [HttpPost("send-to-all")]
    [OpenApiOperation("Send notification to all users", "")]
    [MustHavePermission(FSHAction.Create, FSHResource.Notifications)]
    public Task<string> SendNotificationToAllUsers(SendNotificationRequestToAllUsersRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpPost("get-notifications")]
    [OpenApiOperation("Get notifications", "")]
    public Task<PaginationResponse<NotificationDto>> GetNotifications(GetListNotificationsRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpGet("count-unread")]
    [OpenApiOperation("Count unread notifications", "")]
    public Task<int> CountUnreadNotifications()
    {
        return Mediator.Send(new CountUnreadNotificationsRequest());
    }

    [HttpPut("update-read-status/{id}")]
    [OpenApiOperation("Update read status", "")]
    public Task<string> UpdateReadStatus(Guid id)
    {
        return Mediator.Send(new UpdateNotificationReadStatusRequest(id));
    }

    [HttpPut("read-all")]
    [OpenApiOperation("Read all notifications", "")]
    public Task<string> ReadAllNotifications()
    {
        return Mediator.Send(new ReadAllNotificationsRequest());
    }

    [HttpGet("get-list-users")]
    [OpenApiOperation("Get list users", "")]
    public Task<List<ListUserDto>> GetListUsers()
    {
        return _chatService.GetListUserDtoAsync();
    }

    [HttpGet("get-conversations/{conversionId}")]
    [OpenApiOperation("Get conversations", "")]
    public Task<IEnumerable<ListMessageDto>> GetConversations(string? conversionId, CancellationToken cancellationToken)
    {
        return _chatService.GetConversationAsync(conversionId, cancellationToken);
    }

    [HttpPost("send-message")]
    [OpenApiOperation("Send message", "")]
    public Task SendMessage([FromForm] SendMessageDto send, CancellationToken cancellationToken)
    {
        return _chatService.SendMessageAsync(send, cancellationToken);
    }
}
