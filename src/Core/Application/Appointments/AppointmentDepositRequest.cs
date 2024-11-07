using FSH.WebApi.Application.Identity.WorkingCalendars;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class AppointmentDepositRequest
{
    public string? Key { get; set; }
    public Guid PaymentID { get; set; }
    public Guid AppointmentId { get; set; }
    public double DepositAmount { get; set; }
    public TimeSpan DepositTime { get; set; }
    public bool IsDeposit { get; set; } = false;
}

//public class AppointmentDepositRequestValidator : CustomValidator<AppointmentDepositRequest>
//{
//    public AppointmentDepositRequestValidator(IAppointmentService appointmentService)
//    {
//        RuleFor(p => p.AppointmentId)
//            .NotEmpty()
//            .WithMessage("Appointment Id is required")
//            .When(p => p.IsDeposit)
//            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
//            .When(p => p.IsDeposit)
//            .WithMessage((_, id) => "Appointment is not existing");

//        RuleFor(p => p.DepositAmount)
//            .NotEmpty()
//            .When(p => p.IsDeposit)
//            .WithMessage("Deposit amount is required")
//            .GreaterThan(0)
//            .When(p => p.IsDeposit)
//            .WithMessage("Deposit amount must be greater than 0");

//        RuleFor(p => p.DepositTime)
//            .NotEmpty()
//            .When(p => p.IsDeposit)
//            .WithMessage("Deposit time is required")
//            .Must(time => time > TimeSpan.Zero && time <= TimeSpan.FromMinutes(10))
//            .When(p => p.IsDeposit)
//            .WithMessage("Time is over");

//        RuleFor(p => p)
//        .MustAsync(async (request, cancellation) =>
//               request.IsDeposit)
//        .WithErrorCode(400.ToString())
//        .WithMessage("Appointment is time over");
//    }
//}

//public class AppointmentDepositRequestHandler : IRequestHandler<AppointmentDepositRequest, string>
//{
//    private readonly IAppointmentService _appointmentService;
//    private readonly IStringLocalizer<AppointmentDepositRequest> _t;

//    public AppointmentDepositRequestHandler(IAppointmentService appointmentService, IStringLocalizer<AppointmentDepositRequest> t)
//    {
//        this._appointmentService = appointmentService;
//        _t = t;
//    }

//    public async Task<string> Handle(AppointmentDepositRequest request, CancellationToken cancellationToken)
//    {
//        await _appointmentService.VerifyAndFinishBooking(request, cancellationToken);
//        return _t["Success"];
//    }
//}
