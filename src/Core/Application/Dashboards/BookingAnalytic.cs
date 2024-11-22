using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Dashboards;
public class BookingAnalytic
{
    public DateOnly Date { get; set; }
    public int CancelAnalytic { get; set; }
    public int FailAnalytic { get; set; }
    public int SuccessAnalytic { get; set; }
}
