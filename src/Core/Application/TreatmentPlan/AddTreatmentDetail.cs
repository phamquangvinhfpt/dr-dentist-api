using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.DentalServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FSH.WebApi.Application.Appointments;

namespace FSH.WebApi.Application.TreatmentPlan;
public class AddTreatmentDetail : IRequest<string>
{
    public Guid AppointmentID { get; set; }
    public Guid TreatmentId { get; set; }
    public DateOnly TreatmentDate { get; set; }
    public TimeSpan TreatmentTime { get; set; }
    public string? Note { get; set; }
}

public class AddTreatmentDetailValidator : CustomValidator<AddTreatmentDetail>
{
    public AddTreatmentDetailValidator(ITreatmentPlanService treatmentPlanService, IAppointmentService appointmentService)
    {

        RuleFor(p => p.AppointmentID)
            .NotEmpty()
            .WithMessage("Appointment Information should be include")
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
            .WithMessage("Appointment Not Found");

        RuleFor(p => p.TreatmentId)
            .NotEmpty()
            .WithMessage("Treatment Plan Information should be include")
            .MustAsync(async (id, _) => await treatmentPlanService.CheckPlanExisting(id))
            .WithMessage("Plan Not Found");

        RuleFor(p => p.TreatmentDate)
            .NotNull()
            .WithMessage("Date should be filled")
            .MustAsync(async (date, _) => await treatmentPlanService.CheckDateValid(date))
            .WithMessage((_, date) => $"Date {date} is not valid.");

        RuleFor(p => p.TreatmentTime)
            .NotNull()
            .WithMessage("Start time is required")
            .Must((request, startTime) =>
            {
                var currentTime = DateTime.Now.TimeOfDay;
                var currentDate = DateOnly.FromDateTime(DateTime.Now);

                if (request.TreatmentDate == currentDate)
                {
                    return startTime > currentTime;
                }
                if (startTime < TimeSpan.FromHours(8) || startTime > TimeSpan.FromHours(20))
                {
                    return false;
                }

                return true;
            })
            .WithMessage((request, startTime) =>
            {
                if (startTime < TimeSpan.FromHours(8) || startTime >= TimeSpan.FromHours(20))
                {
                    return "Start time must be between 8:00 AM and 8:00 PM";
                }
                return "Start time must be greater than current time";
            });

        RuleFor(p => p)
            .MustAsync(async (request, _) =>
                await treatmentPlanService.CheckDoctorAvailability(
                    request.TreatmentId,
                    request.TreatmentDate,
                    request.TreatmentTime))
            .WithMessage("Doctor is not available at the selected date and time.");
    }
}

public class AddTreatmentDetailHandler : IRequestHandler<AddTreatmentDetail, string>
{
    private readonly ITreatmentPlanService _treatmentPlanService;
    private readonly IStringLocalizer<AddTreatmentDetailHandler> _t;

    public AddTreatmentDetailHandler(ITreatmentPlanService treatmentPlanService, IStringLocalizer<AddTreatmentDetailHandler> t)
    {
        _treatmentPlanService = treatmentPlanService;
        _t = t;
    }

    public async Task<string> Handle(AddTreatmentDetail request, CancellationToken cancellationToken)
    {
        await _treatmentPlanService.AddFollowUpAppointment(request, cancellationToken);
        return _t["Success"];
    }
}