using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Application.TreatmentPlan.Prescriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace FSH.WebApi.Host.Controllers.TreatmentPlans;
public class TreatmentPlanController : VersionNeutralApiController
{
    private ITreatmentPlanService _treatmentPlanService;

    public TreatmentPlanController(ITreatmentPlanService treatmentPlanService)
    {
        _treatmentPlanService = treatmentPlanService;
    }

    [HttpGet("get/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Get Treatment Plan By Appointment ID", "")]
    public Task<List<TreatmentPlanResponse>> GetTreatmentPlan(Guid id, CancellationToken cancellationToken)
    {
        if (id == null || id == Guid.Empty)
        {
            throw new ArgumentNullException("Patient identity is empty");
        }
        return _treatmentPlanService.GetTreamentPlanByAppointment(id, cancellationToken);
    }

    [HttpPost("add-detail")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Add treatment date and note for next follow up appointment", "")]
    public Task<string> AddTreatmentPlanDetail(AddTreatmentDetail request)
    {
        return Mediator.Send(request);
    }

    [HttpPost("update-detail")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Update treatment date and note for next follow up appointment", "")]
    public Task<string> UpdateTreatmentPlanDetail(AddTreatmentDetail request, CancellationToken cancellationToken)
    {
        return _treatmentPlanService.UpdateTreamentPlan(request, cancellationToken);
    }

    [HttpGet("precsription/add")]
    [OpenApiOperation("Add Prescription", "")]
    public Task<string> AddPrescriptionTreatmentPlan(AddPrescriptionRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpGet("precsription/get/{id}")]
    [OpenApiOperation("Get Prescription By TreatmetnID", "")]
    public Task<PrescriptionResponse> GetPrescriptionByTreatmentID(Guid id, CancellationToken cancellationToken)
    {
        if (id == null || id == Guid.Empty)
        {
            throw new ArgumentNullException("Patient identity is empty");
        }
        return _treatmentPlanService.GetPrescriptionByTreatment(id, cancellationToken);
    }
    [HttpGet("precsription/patient/get/{id}")]
    [OpenApiOperation("Get Prescription By patient id", "")]
    public Task<List<PrescriptionResponse>> GetPrescriptionByPatientID(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentNullException("Patient identity is empty");
        }
        return _treatmentPlanService.GetPrescriptionByPatient(id, cancellationToken);
    }
}
