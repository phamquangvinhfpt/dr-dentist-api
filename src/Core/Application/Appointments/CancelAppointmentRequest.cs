using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class CancelAppointmentRequest : IRequest<string>
{
    public string? UserID { get; set; }
    public Guid AppointmentID { get; set; }
}

public class CancelAppointmentRequestValidator : CustomValidator<CancelAppointmentRequest>
{
    public CancelAppointmentRequestValidator(IUserService userService, IAppointmentService appointmentService)
    {
        RuleFor(p => p.UserID)
            .NotEmpty()
            .WithMessage("Patient Infomation should be include")
            .MustAsync(async (id, _) => await userService.CheckUserInRoleAsync(id, FSHRoles.Patient))
            .WithMessage((_, id) => "User Is Not Found or User Is Not A Patient");

        RuleFor(p => p.AppointmentID)
            .NotEmpty()
            .WithMessage("Appointment Information should be include")
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
            .WithMessage((_, id) => "Appointment is not found");
    }
}

public class CancelAppointmentRequestHandler : IRequestHandler<CancelAppointmentRequest, string>
{
    private readonly IAppointmentService _appointmentService;
    private readonly IStringLocalizer<CancelAppointmentRequest> _t;

    public CancelAppointmentRequestHandler(IAppointmentService appointmentService, IStringLocalizer<CancelAppointmentRequest> t)
    {
        _appointmentService = appointmentService;
        _t = t;
    }

    public async Task<string> Handle(CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        await _appointmentService.CancelAppointment(request, cancellationToken);
        return _t["Success"];
    }
}
