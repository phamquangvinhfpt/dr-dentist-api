using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.AppointmentCalendars;
public class AppointmentCalendarResponse
{
    public Guid DoctorProfileID { get; set; }
    public string? ImageUrl { get; set; }
    public string? UserName { get; set; }
    public List<AppointmentCalendarDetail>? WorkingCalendars { get; set; }

}
