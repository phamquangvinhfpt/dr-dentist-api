using FSH.WebApi.Domain.Notification;

namespace FSH.WebApi.Application.Notifications;
public class UpdateNotificationReadStatusRequest : IRequest<string>
{
    public Guid NotificationId { get; set; }

    public UpdateNotificationReadStatusRequest(Guid notificationId)
    {
        NotificationId = notificationId;
    }
}

public class UpdateNotificationReadStatusRequestSpec : Specification<Notification>
{
    public UpdateNotificationReadStatusRequestSpec(Guid notificationId, Guid userId)
    {
        Query.Where(x => x.Id.Equals(notificationId) && x.UserId.Equals(userId));
    }
}

public class UpdateNotificationReadStatusRequestHandler : IRequestHandler<UpdateNotificationReadStatusRequest, string>
{
    private readonly IRepository<Notification> _notificationRepository;
    private readonly ICurrentUser _currentUser;

    public UpdateNotificationReadStatusRequestHandler(IRepository<Notification> notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(UpdateNotificationReadStatusRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var spec = new UpdateNotificationReadStatusRequestSpec(request.NotificationId, userId);
        var notification = await _notificationRepository.FirstOrDefaultAsync(spec, cancellationToken);

        if (notification == null)
        {
            throw new NotFoundException("Notification not found.");
        }

        notification.UpdateIsRead();

        await _notificationRepository.UpdateAsync(notification, cancellationToken);

        return "Notification read status updated successfully.";
    }
}
