namespace FSH.WebApi.Application.Notifications;
public class SendNotificationRequestToAllUsersRequest : IRequest<string>
{
    public BasicNotification Notification { get; set; }
    public DateTime? SendTime { get; set; }
}

public class SendNotificationRequestHandler : IRequestHandler<SendNotificationRequestToAllUsersRequest, string>
{
    private readonly INotificationService _notificationService;
    public SendNotificationRequestHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<string> Handle(SendNotificationRequestToAllUsersRequest request, CancellationToken cancellationToken)
    {
        _ = _notificationService.SendNotificationToAllUsers(request.Notification, request.SendTime, cancellationToken);
        return "Notification sent";
    }
}
