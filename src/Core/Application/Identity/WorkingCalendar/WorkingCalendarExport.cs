using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class WorkingCalendarExport
{
    public string? Doctor { get; set; }
    public string? TypeWorking { get; set; }
    public string? TypeService { get; set; }
    public string Date { get; set; }
    public string Room { get; set; }
    public string? First_Shift { get; set; }
    public string? First_Status { get; set; }
    public string? Last_Shift { get; set; }
    public string? Second_Status { get; set; }

}
