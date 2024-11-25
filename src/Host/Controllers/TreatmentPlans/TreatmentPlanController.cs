using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Application.TreatmentPlan.Prescriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace FSH.WebApi.Host.Controllers.TreatmentPlans;
public class TreatmentPlanController : VersionNeutralApiController
{
    private ITreatmentPlanService _treatmentPlanService;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUser _currentUserService;
    private static string APPOINTMENT = "APPOINTMENT";
    public TreatmentPlanController(ICacheService cacheService, ITreatmentPlanService treatmentPlanService, ICurrentUser currentUserService)
    {
        _treatmentPlanService = treatmentPlanService;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
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

    [HttpGet("current/get/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Get Current Treatment Plan By Patient ID", "")]
    public Task<List<TreatmentPlanResponse>> GetCurrentTreatmentPlanByPatientID(string id, CancellationToken cancellationToken)
    {
        if (id == null)
        {
            throw new ArgumentNullException("Patient identity is empty");
        }
        return _treatmentPlanService.GetCurrentTreamentPlanByPatientID(id, cancellationToken);
    }

    [HttpPost("add-detail")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Add treatment date and note for next follow up appointment", "")]
    public Task<string> AddTreatmentPlanDetail(AddTreatmentDetail request)
    {
        DeleteRedisCode();
        return Mediator.Send(request);
    }

    [HttpPost("update-detail")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Update treatment date and note for next follow up appointment", "")]
    public Task<string> UpdateTreatmentPlanDetail(AddTreatmentDetail request, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return _treatmentPlanService.UpdateTreamentPlan(request, cancellationToken);
    }

    [HttpPost("precsription/add")]
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

    [HttpGet("examination/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Examination and do Treatment Plan. Change Status Plan use treatment id", "")]
    public Task<string> DoTreatmentPlan(Guid id, CancellationToken cancellationToken)
    {
        if (id == null || id == Guid.Empty)
        {
            throw new ArgumentNullException("Patient identity is empty");
        }
        DeleteRedisCode();
        return _treatmentPlanService.ExaminationAndChangeTreatmentStatus(id, cancellationToken);
    }
    public async Task DeleteRedisCode()
    {
        try
        {
            var keys = await _cacheService.GetAsync<List<string>>(APPOINTMENT);
            foreach (string key in keys)
            {
                _cacheService.Remove(key);
            }
            _cacheService.Remove(APPOINTMENT);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }
}
