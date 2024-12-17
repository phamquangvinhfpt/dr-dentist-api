using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class CalendarDetail
{
    public Guid CalendarID { get; set; }
    public Guid RoomID { get; set; }
    public string? RoomName { get; set; }
    public WorkingStatus WorkingStatus { get; set; }
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public List<TimeDetail>? Times { get; set; }
}
