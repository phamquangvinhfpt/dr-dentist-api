namespace FSH.WebApi.Application.Auditing;

public interface IAuditService : ITransientService
{
    Task<PaginationResponse<AuditDto>> GetUserTrailsAsync(GetMyAuditLogsRequest request);
    Task<List<string?>> GetResourceName();
    Task<Stream> ExportUserTrailsAsync();
}