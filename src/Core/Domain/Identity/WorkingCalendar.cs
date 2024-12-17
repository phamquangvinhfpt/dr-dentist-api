using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Domain.Identity;
public class WorkingCalendar : AuditableEntity, IAggregateRoot
{
    public Guid DoctorID { get; set; }
    public Guid RoomID { get; set; }
    public DateOnly? Date { get; set; }
    public WorkingStatus Status { get; set; }
    public string? Note { get; set; }
}
