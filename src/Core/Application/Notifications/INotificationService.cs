namespace FSH.WebApi.Application.Notifications;
public interface INotificationService : ITransientService
{
    Task SendNotificationToAllUsers(BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken);
    Task SendNotificationToUser(string userId, BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken);
    Task SendNotificationToUsers(List<string> userIds, BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken);
    Task SendPaymentNotificationToUser(string userId, BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken);
}
