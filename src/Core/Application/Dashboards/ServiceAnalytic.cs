using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Dashboards;

public class ServiceAnalytic
{
    public Guid ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public double TotalRevenue { get; set; }
    public double TotalRating { get; set; }
}
