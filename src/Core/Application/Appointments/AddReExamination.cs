using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class AddReExamination
{
    public Guid? DoctorId { get; set; }
    public Guid? PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public DateOnly? Date { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string? Note { get; set; }
}
