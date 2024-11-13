using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class ScheduleAppointmentRequest : IRequest<string>
{
    public Guid AppointmentID { get; set; }
    public string? DoctorID { get; set; }
}

public class ScheduleAppointmentRequestValidator : CustomValidator<ScheduleAppointmentRequest>
{
    public ScheduleAppointmentRequestValidator(IUserService userService, IAppointmentService appointmentService)
    {
        RuleFor(p => p.AppointmentID)
            .NotEmpty()
            .WithMessage("Appointment Information should be included")
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
            .WithMessage((_, id) => "Appointment is not found");

        RuleFor(p => p.DoctorID)
            .NotEmpty()
            .WithMessage("Doctor Information should be included")
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
            .WithMessage((_, id) => $"Patient {id} is not valid.")
            .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Dentist))
            .WithMessage((_, id) => $"User {id} is not dentist.");
    }
}

public class ScheduleAppointmentRequestHandler : IRequestHandler<ScheduleAppointmentRequest, string>
{
    private readonly IAppointmentService appointmentService;
    private readonly IStringLocalizer<ScheduleAppointmentRequest> _t;

    public ScheduleAppointmentRequestHandler(IAppointmentService appointmentService, IStringLocalizer<ScheduleAppointmentRequest> t)
    {
        this.appointmentService = appointmentService;
        _t = t;
    }

    public async Task<string> Handle(ScheduleAppointmentRequest request, CancellationToken cancellationToken)
    {
        await appointmentService.ScheduleAppointment(request, cancellationToken);
        return _t["Success"];
    }
}