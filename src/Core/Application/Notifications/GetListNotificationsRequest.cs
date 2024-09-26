using FSH.WebApi.Domain.Notification;

namespace FSH.WebApi.Application.Notifications;
public class GetListNotificationsRequest : PaginationFilter, IRequest<PaginationResponse<NotificationDto>>
{
    public Guid? UserId { get; set; }
    public bool? IsRead { get; set; }
}

public class GetListNotificationsRequestSpec : EntitiesByPaginationFilterSpec<Notification, NotificationDto>
{
    public GetListNotificationsRequestSpec(GetListNotificationsRequest request) : base(request)
    {
        Query.Where(x => x.UserId.Equals(request.UserId))
             .Where(x => x.IsRead == request.IsRead!.Value, request.IsRead.HasValue)
             .OrderByDescending(n => n.CreatedOn);
    }
}

public class GetListNotificationsRequestHandler : IRequestHandler<GetListNotificationsRequest, PaginationResponse<NotificationDto>>
{
    private readonly IReadRepository<Notification> _repository;
    private readonly ICurrentUser _currentUser;

    public GetListNotificationsRequestHandler(IReadRepository<Notification> repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<PaginationResponse<NotificationDto>> Handle(GetListNotificationsRequest request, CancellationToken cancellationToken)
    {
        request.UserId = _currentUser.GetUserId();
        var spec = new GetListNotificationsRequestSpec(request);
        return await _repository.PaginatedListAsync(spec, request.PageNumber, request.PageSize, cancellationToken: cancellationToken);
    }
}
