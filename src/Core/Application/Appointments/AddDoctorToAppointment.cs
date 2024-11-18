using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Appointments;
public class AddDoctorToAppointment
{
    public Guid DoctorID { get; set; }
    public Guid AppointmentID { get; set; }
}
