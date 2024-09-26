namespace FSH.WebApi.Application.Notifications;
public class TestNotificationRequest : IRequest<string>
{
}

public class TestNotificationHandler : IRequestHandler<TestNotificationRequest, string>
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public TestNotificationHandler(INotificationService notificationService, ICurrentUser currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(TestNotificationRequest request, CancellationToken cancellationToken)
    {
        string userId = _currentUser.GetUserId().ToString();
        var notification = new BasicNotification
        {
            Message = "Hello, World!",
            Label = BasicNotification.LabelType.Information,
            Title = "Test Notification",
            Url = "/test"
        };

        await _notificationService.SendNotificationToUser(userId, notification, null, cancellationToken);

        return "Notification sent";
    }
}