using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class AddRoomToWorkingRequest
{
    public Guid CalendarID { get; set; }
    public Guid RoomID { get; set; }
}
