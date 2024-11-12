using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.TreatmentPlan;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        return _treatmentPlanService.GetTreamentPlanByAppointment(id, cancellationToken);
    }

    [HttpPost("add-detail")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Add treatment date and note for next follow up appointment", "")]
    public Task<string> GetTreatmentPlan(AddTreatmentDetail request)
    {
        return Mediator.Send(request);
    }
}
