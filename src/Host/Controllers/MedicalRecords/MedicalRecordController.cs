using FSH.WebApi.Application.MedicalRecords;

namespace FSH.WebApi.Host.Controllers.MedicalRecords;

public class MedicalRecordController : VersionNeutralApiController
{
    private readonly IMedicalRecordService _mediicalRecordService;

    public MedicalRecordController(IMedicalRecordService mediicalRecordService)
    {
        _mediicalRecordService = mediicalRecordService;
    }

    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.MedicalRecord)]
    [OpenApiOperation("Create Medical record.", "")]
    public Task<string> CreateMedicalRecord([FromForm] CreateMedicalRecordRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpGet("get/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.MedicalRecord)]
    [OpenApiOperation("Get Medical record by ID", "")]
    public Task<MedicalRecordResponse> GetMedicalRecordByID(Guid id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetMedicalRecordByID(id, cancellationToken);
    }

    [HttpGet("get-by-appointment/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.MedicalRecord)]
    [OpenApiOperation("Get Medical record by appointment ID", "")]
    public Task<MedicalRecordResponse> GetMedicalRecordByAppointmentID(Guid id, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetMedicalRecordByAppointmentID(id, cancellationToken);
    }

    [HttpGet("patient/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.MedicalRecord)]
    [OpenApiOperation("Get Medical records by patient ID", "")]
    public Task<List<MedicalRecordResponse>> GetMedicalRecordsByPatientID(string id, DateOnly SDate, DateOnly EDate, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetMedicalRecordsByPatientId(id, SDate, EDate, cancellationToken);
    }

    [HttpPost("get-all")]
    [OpenApiOperation("Get all Medical record.", "")]
    public Task<PaginationResponse<MedicalRecordResponse>> GetAllMedicalRecord(PaginationFilter request, CancellationToken cancellationToken)
    {
        return _mediicalRecordService.GetAllMedicalRecord(request, cancellationToken);
    }
}