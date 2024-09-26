using FSH.WebApi.Domain.Appointments;

namespace FSH.WebApi.Domain.Payments;

public class Payment : AuditableEntity, IAggregateRoot
{
    public string? PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }

    public Appointment? Appointment { get; set; }

    public Payment()
    {
    }

    public Payment(string? patientId, Guid? appointmentId, decimal amount, PaymentMethod method, PaymentStatus status)
    {
        PatientId = patientId;
        AppointmentId = appointmentId;
        Amount = amount;
        Method = method;
        Status = status;
    }
}
