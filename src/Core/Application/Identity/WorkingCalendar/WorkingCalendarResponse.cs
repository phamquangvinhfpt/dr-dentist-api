using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class WorkingCalendarResponse
{
    public string? DentistUserID { get; set; }
    public Guid DentistProfileId { get; set; }
    public string? DentistName { get; set; }
    public string? DentistImage { get; set; }
    public string? Phone { get; set; }
    public WorkingType WorkingType { get; set; }
    public List<CalendarDetail>? CalendarDetails { get; set; }
}
