using FSH.WebApi.Application.Auditing;
using FSH.WebApi.Application.Common.FileStorage;

namespace FSH.WebApi.Host.Controllers.File;

public class FilesController : VersionedApiController
{
    private readonly IAuditService _auditService;
    public FilesController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    // Upload all files with no limit for Admins
    [HttpPost("upload-multiple")]
    [OpenApiOperation("Upload multiple files no limit.", "")]
    [MustHavePermission(FSHAction.Upload, FSHResource.Files)]
    public async Task<string[]> UploadMultipleAsync([FromForm] MultipleFileUploadRequest request)
    {
        return await Mediator.Send(request);
    }

    [HttpDelete("delete-multiple")]
    [OpenApiOperation("Delete multiple file.", "")]
    [MustHavePermission(FSHAction.Upload, FSHResource.Files)]
    public async Task<string> DeleteAsync(MultipleFileDeleteRequest request)
    {
        return await Mediator.Send(request);
    }

    [HttpGet("test")]
    public async Task<string> TestAsync()
    {
        return "Test";
    }

    [HttpPost("export-audit-logs")]
    [OpenApiOperation("Export audit logs.", "")]
    [MustHavePermission(FSHAction.Export, FSHResource.Files)]
    public async Task<FileResult> ExportAuditLogs()
    {
        var stream = await _auditService.ExportUserTrailsAsync();
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "AuditLogs.xlsx");
    }
}
