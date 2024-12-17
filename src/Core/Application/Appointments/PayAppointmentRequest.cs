using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Domain.Payments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class PayAppointmentRequest : IRequest<string>
{
    public string? Key { get; set; }
    public Guid PaymentID { get; set; }
    public Guid AppointmentId { get; set; }
    public string? PatientCode { get; set; }
    public double Amount { get; set; }
    public TimeSpan Time { get; set; }
    public PaymentMethod Method { get; set; }
    public bool IsVerify { get; set; } = false;
    public bool IsPay { get; set; } = false;
    public bool IsCancel { get; set; } = false;
}

public class PayAppointmentRequestValidator : CustomValidator<PayAppointmentRequest>
{
    public PayAppointmentRequestValidator(IAppointmentService appointmentService, IPaymentService paymentService)
    {
        RuleFor(p => p.AppointmentId)
            .NotEmpty()
            .When(p => !p.IsCancel)
            .WithMessage("Appointment Id is required")
            .MustAsync(async (id, _) => await appointmentService.CheckAppointmentExisting(id))
            .When(p => !p.IsCancel)
            .WithMessage((_, id) => "Appointment is not existing");

        RuleFor(p => p.PaymentID)
            .NotEmpty()
            .When(p => !p.IsCancel)
            .WithMessage("Payment Informaiton is required")
            .MustAsync(async (id, _) => await paymentService.CheckPaymentExisting(id))
            .When(p => !p.IsCancel)
            .WithMessage((_, id) => "Payment information is not existing");

        RuleFor(p => p.Amount)
            .NotEmpty()
            .When(p => !p.IsCancel)
            .WithMessage("Deposit amount is required")
            .GreaterThan(0)
            .When(p => !p.IsCancel)
            .WithMessage("Deposit amount must be greater than 0");

        RuleFor(p => p.Time)
            .NotEmpty()
            .When(p => !p.IsPay && !p.IsCancel)
            .WithMessage("Time is required")
            .Must(time => time > TimeSpan.Zero && time <= TimeSpan.FromMinutes(10))
            .When(p => !p.IsPay && !p.IsCancel)
            .WithMessage("Time is over");
    }
}

public class PayAppointmentRequestHandler : IRequestHandler<PayAppointmentRequest, string>
{
    private readonly IAppointmentService _appointmentService;
    private readonly IStringLocalizer<PayAppointmentRequest> _t;

    public PayAppointmentRequestHandler(IAppointmentService appointmentService, IStringLocalizer<PayAppointmentRequest> t)
    {
        _appointmentService = appointmentService;
        _t = t;
    }

    public async Task<string> Handle(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        await _appointmentService.HandlePaymentRequest(request, cancellationToken);
        return _t["Success"];
    }
}
