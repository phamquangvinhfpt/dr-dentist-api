using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.CustomerServices.Feedbacks;
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
    Task<List<BookingAnalytic>> BookingAnalytics(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<int> SatisfiedPatientAsync(CancellationToken cancellationToken);
    Task<int> RegularDoctorAsync(CancellationToken cancellationToken);
    Task<int> TotalServiceAsync(CancellationToken cancellationToken);
    Task<int> AppointmentDoneAsync(CancellationToken cancellationToken);
    Task<List<FeedbackServiceDetail>> PatientFeedbacksAsync(CancellationToken cancellationToken);
    Task<int> TotalAppointmentsAsync(DateOnly date, CancellationToken cancellationToken);
    Task<int> NewContactsAsync(DateOnly date, CancellationToken cancellationToken);
    Task<PaginationResponse<AppointmentResponse>> GetAppointmentAsync(DateOnly date, PaginationFilter filter, CancellationToken cancellationToken);
}
