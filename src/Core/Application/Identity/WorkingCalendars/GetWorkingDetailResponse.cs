using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendars;
public class GetWorkingDetailResponse
{
    public Guid CalendarID { get; set; }
    public string? PatientName { get; set; }
    public string? PatientCode { get; set; }
    public Guid PatientProfileID { get; set; }
    public Guid DoctorProfileID { get; set; }
    public string? DoctorName { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid ServiceID { get; set; }
    public string? ServiceName { get; set; }
    public Guid ProcedureID { get; set; }
    public string? ProcedureName { get; set; }
    public DateOnly Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public CalendarStatus Status { get; set; }
    public string? Note { get; set; }
    public AppointmentType? AppointmentType { get; set; }
    public int Step { get; set; }
}
