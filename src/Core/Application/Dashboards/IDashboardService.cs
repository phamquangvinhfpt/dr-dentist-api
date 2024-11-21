using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.Dashboards;
public interface IDashboardService : ITransientService
{
    Task<List<AnalyticChart>> GetRevenueChartForAdmin(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<List<AnalyticChart>> GetNewDepositBooking(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<List<AnalyticChart>> MemberShipGrowth(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<List<ServiceAnalytic>> ServiceAnalytic(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<List<DoctorAnalytic>> DoctorAnalytic(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
}
