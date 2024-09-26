
namespace FSH.WebApi.Application.Auditing;

public class GetMyAuditLogsRequest : IRequest<PaginationResponse<AuditDto>>
{
    public Guid UserId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Action { get; set; }
    public string? Resource { get; set; }
}

public class GetMyAuditLogsRequestHandler : IRequestHandler<GetMyAuditLogsRequest, PaginationResponse<AuditDto>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public GetMyAuditLogsRequestHandler(ICurrentUser currentUser, IAuditService auditService) =>
        (_currentUser, _auditService) = (currentUser, auditService);

    public async Task<PaginationResponse<AuditDto>> Handle(GetMyAuditLogsRequest request, CancellationToken cancellationToken)
    {
        request.UserId = _currentUser.GetUserId();
        return await _auditService.GetUserTrailsAsync(request);
    }
}