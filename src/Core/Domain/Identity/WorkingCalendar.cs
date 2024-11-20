using FSH.WebApi.Domain.Appointments;

namespace FSH.WebApi.Domain.Identity;

public class WorkingCalendar : AuditableEntity, IAggregateRoot
{
    public Guid? DoctorId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? PlanID { get; set; }
    public DateOnly? Date { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public CalendarStatus Status { get; set; }
    public string? Note { get; set; }
    public AppointmentType Type { get; set; }
}