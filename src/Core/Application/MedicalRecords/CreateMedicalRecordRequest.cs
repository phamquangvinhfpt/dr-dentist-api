using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.MedicalRecords;
public class CreateMedicalRecordRequest : IRequest<string>
{
    public Guid AppointmentId { get; set; }

    // Basic Examination
    public BasicExaminationRequest? BasicExamination { get; set; }

    // Diagnosis
    public List<DiagnosisRequest>? Diagnosis { get; set; }

    // Indication
    public IndicationRequest? Indication { get; set; }

    public List<IndicationImageRequest>? IndicationImages { get; set; }
}

public class CreateMedicalRecordValidator : CustomValidator<CreateMedicalRecordRequest>
{
    public CreateMedicalRecordValidator(IUserService userService, ICurrentUser currentUser, IAppointmentService appointmentService, IMedicalRecordService medicalRecordService)
    {
        //RuleFor(p => p.PatientId)
        //    .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
        //    .When(p => p.PatientId != null)
        //    .WithMessage((_, id) => $"Patient {id} is not valid.")
        //    .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Patient))
        //    .When(p => p.PatientId != null)
        //    .WithMessage((_, id) => $"User {id} is not patient.");

        //RuleFor(p => p.DoctorId)
        //    .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
        //    .When(p => p.DoctorId != null)
        //    .WithMessage((_, id) => $"Patient {id} is not valid.")
        //    .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Dentist))
        //    .When(p => p.DoctorId != null)
        //    .WithMessage((_, id) => $"User {id} is not dentist.");

        RuleFor(x => x.AppointmentId)
            .NotNull()
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
            .WithMessage((_, id) => $"Appointment {id} is not valid.");

        RuleFor(x => x.Diagnosis)
            .NotEmpty()
            .WithMessage("Dianosis information should be include");

        RuleForEach(x => x.Diagnosis)
            .SetValidator(new DiagnosisRequestValidator(medicalRecordService));

        RuleFor(x => x.BasicExamination)
            .SetValidator(new BasicExaminationValidator());

        RuleFor(x => x.Indication)
            .NotEmpty()
            .WithMessage("Indication information should be include")
            .SetValidator(new IndicationRequestValidator());

        RuleFor(x => x.IndicationImages)
            .ForEach(item => item.SetValidator(new IndicationImageRequestValidator())).When(p => p.IndicationImages != null);
    }
}

public class CreateMedicalRecordRequestHandler : IRequestHandler<CreateMedicalRecordRequest, string>
{
    private readonly IMedicalRecordService _mediicalRecordService;
    private readonly IStringLocalizer<CreateMedicalRecordRequest> _t;

    public CreateMedicalRecordRequestHandler(IMedicalRecordService mediicalRecordService, IStringLocalizer<CreateMedicalRecordRequest> t)
    {
        _mediicalRecordService = mediicalRecordService;
        _t = t;
    }

    public async Task<string> Handle(CreateMedicalRecordRequest request, CancellationToken cancellationToken)
    {
        await _mediicalRecordService.CreateMedicalRecord(request, cancellationToken);
        return _t["Medical record updated successfully."];
    }
}
