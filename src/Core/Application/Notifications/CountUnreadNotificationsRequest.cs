using FSH.WebApi.Domain.Notification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Notifications;
public class CountUnreadNotificationsRequest : IRequest<int>
{
}

public class CountUnreadNotificationsRequestSpec : Specification<Notification>
{
    public CountUnreadNotificationsRequestSpec(Guid userId)
    {
        Query.Where(x => !x.IsRead && x.UserId.Equals(userId));
    }
}

public class CountUnreadNotificationsRequestHandler : IRequestHandler<CountUnreadNotificationsRequest, int>
{
    private readonly IRepository<Notification> _notificationRepository;
    private readonly ICurrentUser _currentUser;

    public CountUnreadNotificationsRequestHandler(IRepository<Notification> notificationRepository, ICurrentUser currentUser)
    {
        _notificationRepository = notificationRepository;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(CountUnreadNotificationsRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetUserId();
        var spec = new CountUnreadNotificationsRequestSpec(userId);
        return await _notificationRepository.CountAsync(spec, cancellationToken);
    }
}
