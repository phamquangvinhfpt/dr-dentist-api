using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.TreatmentPlan;
public interface ITreatmentPlanService : ITransientService
{
    Task AddFollowUpAppointment(AddTreatmentDetail request, CancellationToken cancellationToken);
    Task<bool> CheckDateValid(DateOnly date);
    Task<bool> CheckDoctorAvailability(DefaultIdType treatmentId, DateOnly treatmentDate, TimeSpan treatmentTime);
    Task<bool> CheckPlanExisting(Guid id);
    Task<List<TreatmentPlanResponse>> GetTreamentPlanByAppointment(Guid appointmentId, CancellationToken cancellationToken);
    Task UpdateTreamentPlan();
}
