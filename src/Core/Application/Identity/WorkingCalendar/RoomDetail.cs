using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class RoomDetail
{
    public Guid RoomID { get; set; }
    public string? RoomName { get; set; }
    public bool Status { get; set; }
    public string? DoctorID { get; set; }
    public string? DoctorName { get; set; }
    public DateOnly CreateDate { get; set; }
}
