using FSH.WebApi.Application.Identity.AppointmentCalendars;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class RescheduleRequest : IRequest<string>
{
    public Guid AppointmentID { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan Duration { get; set; }
}

public class RescheduleRequestValidator : CustomValidator<RescheduleRequest>
{
    public RescheduleRequestValidator(IAppointmentService appointmentService, IAppointmentCalendarService workingCalendarService)
    {
        RuleFor(p => p.AppointmentID)
            .NotEmpty()
            .WithMessage("Need Appointment Information To Reschedule.")
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
            .WithMessage((_, id) => $"Appointment {id} is not found")
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentAvailableToReschedule(id))
            .WithMessage((_, id) => $"Appointment {id} is failed");

        RuleFor(p => p.AppointmentDate)
            .NotEmpty()
            .WithMessage("Need Appointment Date To Reschedule.")
            .MustAsync(async (date, _) => await appointmentService.CheckAppointmentDateValid(date))
            .WithMessage((_, date) => $"Day {date} is not valid to reschedule.");

        RuleFor(p => p.StartTime)
            .NotNull()
            .WithMessage("Start time is required")
            .Must((request, startTime) =>
            {
                var currentTime = DateTime.Now.TimeOfDay;
                var currentDate = DateOnly.FromDateTime(DateTime.Now);

                if (request.AppointmentDate == currentDate)
                {
                    return startTime > currentTime;
                }
                if (startTime < TimeSpan.FromHours(8) || startTime > TimeSpan.FromHours(22))
                {
                    return false;
                }
                return true;
            })
            .WithMessage((request, startTime) =>
            {
                if (startTime < TimeSpan.FromHours(8) || startTime > TimeSpan.FromHours(22))
                {
                     return "Start time must be between 8:00 AM and 10:00 PM";
                }
                return "Start time must be greater than current time";
            });
        RuleFor(p => p.Duration)
           .NotNull()
           .WithMessage("Duration is required")
           .Must(duration => duration >= TimeSpan.FromMinutes(30) && duration <= TimeSpan.FromHours(1))
           .WithMessage("Duration must be between 30 minutes and 1 hours")
           .Must((request, duration) =>
               (request.StartTime + duration) <= TimeSpan.FromHours(22))
           .WithMessage("Appointment must end before 10:00 PM");
    }
}

public class RescheduleRequestHandler : IRequestHandler<RescheduleRequest, string>
{
    private readonly IAppointmentService appointmentService;
    private readonly IStringLocalizer<RescheduleRequest> _t;

    public RescheduleRequestHandler(IAppointmentService appointmentService, IStringLocalizer<RescheduleRequest> t)
    {
        this.appointmentService = appointmentService;
        _t = t;
    }

    public async Task<string> Handle(RescheduleRequest request, CancellationToken cancellationToken)
    {
        await appointmentService.RescheduleAppointment(request, cancellationToken);
        return _t["Success"];
    }
}
