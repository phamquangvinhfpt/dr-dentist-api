using FSH.WebApi.Application.Identity.WorkingCalendar;
using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.ApplicationForms;
public class FormDetailResponse
{
    public string? UserID { get; set; }
    public string? Name { get; set; }
    public Guid CalendarID { get; set; }
    public DateOnly WorkingDate { get; set; }
    public List<TimeDetail>? WorkingTimes { get; set; }
    public string? Description { get; set; }
    public string? Note { get; set; }
    public FormStatus Status { get; set; }
}
