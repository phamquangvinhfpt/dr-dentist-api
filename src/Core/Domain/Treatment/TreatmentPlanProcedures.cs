using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Examination;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using System.Text.Json.Serialization;

namespace FSH.WebApi.Domain.Treatment;

public class TreatmentPlanProcedures : AuditableEntity, IAggregateRoot
{
    public Guid? ServiceProcedureId { get; set; }
    public Guid? AppointmentID { get; set; }
    public Guid? DoctorID { get; set; }
    public TreatmentPlanStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public double Price { get; set; }
    public double DiscountAmount { get; set; }
    public double TotalCost { get; set; }
    public string? Note { get; set; }
    public int RescheduleTime { get; set; } = 0;


    // navigation properties
    [JsonIgnore]
    public Appointment? Appointment { get; set; }
    [JsonIgnore]
    public ServiceProcedures? ServiceProcedure { get; set; }
    [JsonIgnore]
    public PaymentDetail? PaymentDetail { get; set; }
    [JsonIgnore]
    public Prescription? Prescription { get; set; }
    public TreatmentPlanProcedures()
    {
    }

    public TreatmentPlanProcedures(DefaultIdType? serviceProcedureId, DefaultIdType? appointmentID, DefaultIdType? doctorID, TreatmentPlanStatus status, DateOnly? startDate, TimeSpan? startTime, double price, double discountAmount, double totalCost, string? note, int rescheduleTime)
    {
        ServiceProcedureId = serviceProcedureId;
        AppointmentID = appointmentID;
        DoctorID = doctorID;
        Status = status;
        StartDate = startDate;
        StartTime = startTime;
        Price = price;
        DiscountAmount = discountAmount;
        TotalCost = totalCost;
        Note = note;
        RescheduleTime = rescheduleTime;
    }
}
