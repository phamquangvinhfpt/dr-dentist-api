using FSH.WebApi.Application.TreatmentPlan.Prescriptions;
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
    Task<string> UpdateTreamentPlan(AddTreatmentDetail request, CancellationToken cancellationToken);
    Task AddPrescription(AddPrescriptionRequest request, CancellationToken cancellationToken);
    Task<PrescriptionResponse> GetPrescriptionByTreatment(Guid id, CancellationToken cancellationToken);
    Task<List<PrescriptionResponse>> GetPrescriptionByPatient(string id, CancellationToken cancellationToken);
    Task<string> ExaminationAndChangeTreatmentStatus(DefaultIdType id, CancellationToken cancellationToken);
    Task<List<TreatmentPlanResponse>> GetCurrentTreamentPlanByPatientID(string id, CancellationToken cancellationToken);
}
