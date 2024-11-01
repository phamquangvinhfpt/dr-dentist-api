using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Payments;

public class Payment : AuditableEntity, IAggregateRoot
{
    public Guid? PatientProfileId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? ServiceId { get; set; }
    public double? DepositAmount { get; set; }
    public DateOnly? DepositDate { get; set; }
    public double? RemainingAmount { get; set; }
    public DateOnly? RemainingDate { get; set; }
    public DateOnly? FinalPaymentDate { get; set; }
    public double? Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }

    // navigation properties
    [JsonIgnore]
    public Appointment? Appointment { get; set; }
    [JsonIgnore]
    public Service.Service? Service { get; set; }
    [JsonIgnore]
    public PatientProfile? PatientProfile { get; set; }
    [JsonIgnore]
    public List<PaymentDetail> PaymentDetails { get; set; }
    public Payment()
    {
    }

    public Payment(Guid? patientProfileId, Guid? appointmentId, Guid? serviceId, double? depositAmount, DateOnly? depositDate, double? remainingAmount, DateOnly? remainingDate, DateOnly? finalPaymentDate, double? amount, PaymentMethod method, PaymentStatus status)
    {
        PatientProfileId = patientProfileId;
        AppointmentId = appointmentId;
        ServiceId = serviceId;
        DepositAmount = depositAmount;
        DepositDate = depositDate;
        RemainingAmount = remainingAmount;
        RemainingDate = remainingDate;
        FinalPaymentDate = finalPaymentDate;
        Amount = amount;
        Method = method;
        Status = status;
    }
}
