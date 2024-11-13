using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Common.Persistence;
using FSH.WebApi.Application.Notifications;
using FSH.WebApi.Domain.Notification;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Shared.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FSH.WebApi.Infrastructure.Notifications;
public class NotificationService : INotificationService
{
    private readonly IRepository<Notification> _notificationRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationSender _notificationSender;
    private readonly IJobService _jobService;

    public NotificationService(
            IRepository<Notification> notificationRepository,
            UserManager<ApplicationUser> userManager,
            INotificationSender notificationSender,
            IJobService jobService)
    {
        _notificationRepository = notificationRepository;
        _userManager = userManager;
        _notificationSender = notificationSender;
        _jobService = jobService;
    }

    public async Task SendNotificationToAllUsers(BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken)
    {
        if (sendTime != null && sendTime > DateTime.Now)
        {
            TimeSpan timeSpan = sendTime.Value - DateTime.Now;
            _jobService.Schedule(() => ExcuteSendNotificationToAllUsers(notification, cancellationToken), timeSpan);
        }
        else
        {
            _jobService.Enqueue(() => ExcuteSendNotificationToAllUsers(notification, cancellationToken));
        }
    }

    public async Task ExcuteSendNotificationToAllUsers(BasicNotification notification, CancellationToken cancellationToken)
    {
        List<string> userIds = await _userManager.Users.Select(u => u.Id).ToListAsync(cancellationToken);
        await ExcuteSendNotificationToUsers(userIds, notification, cancellationToken);
    }

    public async Task SendNotificationToUser(string userId, BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken)
    {
        if (sendTime != null && sendTime > DateTime.Now)
        {
            TimeSpan timeSpan = sendTime.Value - DateTime.Now;
            _jobService.Schedule(() => ExcuteSendNotificationToUser(userId, notification, cancellationToken), timeSpan);
        }
        else
        {
            _jobService.Enqueue(() => ExcuteSendNotificationToUser(userId, notification, cancellationToken));
        }
    }

    public async Task ExcuteSendNotificationToUser(string userId, BasicNotification notification, CancellationToken cancellationToken)
    {
        Notification addNoti = new Notification(
             Guid.Parse(userId),
             notification.Title,
             notification.Label,
             notification.Message,
             notification.Url);
        await _notificationRepository.AddAsync(addNoti, cancellationToken);
        await _notificationSender.SendToUserAsync(notification, userId, cancellationToken);
    }

    public async Task SendNotificationToUsers(List<string> userIds, BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken)
    {
        if (sendTime != null && sendTime > DateTime.Now)
        {
            TimeSpan timeSpan = sendTime.Value - DateTime.Now;
            _jobService.Schedule(() => ExcuteSendNotificationToUsers(userIds, notification, cancellationToken), timeSpan);
        }
        else
        {
            _jobService.Enqueue(() => ExcuteSendNotificationToUsers(userIds, notification, cancellationToken));
        }
    }

    public async Task ExcuteSendNotificationToUsers(List<string> userIds, BasicNotification notification, CancellationToken cancellationToken)
    {
        List<Notification> addNotis = new List<Notification>();
        foreach (string userId in userIds)
        {
            addNotis.Add(new Notification(
                            Guid.Parse(userId),
                            notification.Title,
                            notification.Label,
                            notification.Message,
                            notification.Url));
        }

        await _notificationRepository.AddRangeAsync(addNotis, cancellationToken);
        await _notificationSender.SendToUsersAsync(notification, userIds, cancellationToken);
    }

    public async Task SendPaymentNotificationToUser(string userId, BasicNotification notification, DateTime? sendTime, CancellationToken cancellationToken)
    {
        if (sendTime != null && sendTime > DateTime.Now)
        {
            TimeSpan timeSpan = sendTime.Value - DateTime.Now;
            _jobService.Schedule(() => ExcuteSendPaymentNotificationToUser(userId, notification, cancellationToken), timeSpan);
        }
        else
        {
            _jobService.Enqueue(() => ExcuteSendPaymentNotificationToUser(userId, notification, cancellationToken));
        }
    }

    public async Task ExcuteSendPaymentNotificationToUser(string userId, BasicNotification notification, CancellationToken cancellationToken)
    {
        Notification addNoti = new Notification(
             Guid.Parse(userId),
             notification.Title,
             notification.Label,
             notification.Message,
             notification.Url);
        await _notificationRepository.AddAsync(addNoti, cancellationToken);
        await _notificationSender.SendPaymentNotificationToUserAsync(notification, userId, cancellationToken);
    }
}
