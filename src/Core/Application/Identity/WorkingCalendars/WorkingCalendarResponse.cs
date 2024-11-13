using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendars;
public class WorkingCalendarResponse
{
    public Guid DoctorProfileID { get; set; }
    public string? ImageUrl { get; set; }
    public string? UserName { get; set; }
    public List<WorkingCalendarDetail>? WorkingCalendars { get; set; }

}
