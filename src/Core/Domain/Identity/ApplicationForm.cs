using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Domain.Identity;
public class ApplicationForm : AuditableEntity, IAggregateRoot
{
    public string? UserID { get; set; }
    public Guid CalendarID { get; set; }
    public Guid TimeID { get; set; }
    public string? Description { get; set; }
    public string? Note { get; set; }
    public FormStatus Status { get; set; }
}
