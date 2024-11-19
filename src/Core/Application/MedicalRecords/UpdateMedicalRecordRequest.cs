using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.MedicalRecords;
public class UpdateMedicalRecordRequest : IRequest<string>
{
    public Guid RecordID { get; set; }

    // Basic Examination
    public BasicExaminationRequest? BasicExamination { get; set; }

    // Diagnosis
    public DiagnosisRequest? Diagnosis { get; set; }

    // Indication
    public IndicationRequest? Indication { get; set; }

    // Indication Images
    public List<IndicationImageRequest>? IndicationImages { get; set; }
}

public class UpdateMedicalRecordValidator : CustomValidator<UpdateMedicalRecordRequest>
{
    public UpdateMedicalRecordValidator(IUserService userService, ICurrentUser currentUser, IAppointmentService appointmentService)
    {
        RuleFor(x => x.Diagnosis)
            .SetValidator(new DiagnosisRequestValidator());

        RuleFor(x => x.BasicExamination)
            .SetValidator(new BasicExaminationValidator());

        RuleFor(x => x.Indication)
            .SetValidator(new IndicationRequestValidator());

        RuleFor(x => x.IndicationImages)
            .ForEach(item => item.SetValidator(new IndicationImageRequestValidator()));
    }
}

public class UpdateMedicalRecordRequestHandler : IRequestHandler<UpdateMedicalRecordRequest, string>
{
    private readonly IMedicalRecordService _mediicalRecordService;
    private readonly IStringLocalizer<UpdateMedicalRecordRequest> _t;

    public UpdateMedicalRecordRequestHandler(IMedicalRecordService mediicalRecordService, IStringLocalizer<UpdateMedicalRecordRequest> t)
    {
        _mediicalRecordService = mediicalRecordService;
        _t = t;
    }

    public async Task<string> Handle(UpdateMedicalRecordRequest request, CancellationToken cancellationToken)
    {
        await _mediicalRecordService.UpdateMedicalRecord(request, cancellationToken);
        return _t["Medical record updated successfully."];
    }
}
