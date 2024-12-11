using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Identity.WorkingCalendar;
public class WeeklyData
{
    public int Hours { get; set; }
    public DateTime FirstDate { get; set; }
    public DateTime LastDate { get; set; }
    public int WorkDays { get; set; }
}
