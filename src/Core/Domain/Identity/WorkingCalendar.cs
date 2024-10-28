namespace FSH.WebApi.Domain.Identity;

public class WorkingCalendar : AuditableEntity, IAggregateRoot
{
    public string? DoctorId { get; set; }
    public DateOnly? Date { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
}