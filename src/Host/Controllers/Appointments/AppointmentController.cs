using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.AppointmentCalendars;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Application.TreatmentPlan;
using FSH.WebApi.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.Appointments;
public class AppointmentController : VersionNeutralApiController
{
    private readonly IAppointmentService _appointmentService;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUser _currentUserService;
    private static string APPOINTMENT = "APPOINTMENT";
    private static string FOLLOW = "FOLLOW";
    private static string REEXAM = "REEXAM";
    private static string NON = "NON";
    public AppointmentController(ICacheService cacheService, IAppointmentService appointmentService, ICurrentUser currentUserService)
    {
        _appointmentService = appointmentService;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }
    //checked
    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Appointment)]
    [OpenApiOperation("Create Appointment", "")]
    public Task<PayAppointmentRequest> CreateAppointment(CreateAppointmentRequest request)
    {
        DeleteRedisCode();
        return Mediator.Send(request);
    }
    //checked
    [HttpPost("re/create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Appointment)]
    [OpenApiOperation("Create ReExamination Appointment", "")]
    public Task<string> CreateReAppointment(AddReExamination request, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return _appointmentService.CreateReExamination(request, cancellationToken);
    }

    //checked
    [HttpPost("doctor/available")]
    [OpenApiOperation("Get Doctors Available for service at Date and Time", "")]
    public async Task<List<GetDoctorResponse>> GetAvailableDoctor(GetAvailableDoctor request, CancellationToken cancellationToken)
    {
        //DeleteRedisCode();
        return await _appointmentService.GetAvailableDoctorAsync(request, cancellationToken);
    }

    [HttpPost("cancel")]
    [MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Cancel Appointment", "")]
    public Task<string> CancelAppointment(CancelAppointmentRequest request)
    {
        DeleteRedisCode();
        return Mediator.Send(request);
    }

    [HttpPost("reschedule")]
    [MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Reschedule Appointment", "")]
    public Task<string> RescheduleAppointment(RescheduleRequest request)
    {
        DeleteRedisCode();
        return Mediator.Send(request);
    }
    //checked
    [HttpPost("get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Appointments", "")]
    public async Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        string key = RedisKeyGenerator.GenerateAppointmentKey(
        _currentUserService.GetUserId().ToString(),
        filter,
        date,
        APPOINTMENT
    );
        var r = _cacheService.Get<PaginationResponse<AppointmentResponse>>(key);
        if (r != null) {
            return r;
        }
        var result = await _appointmentService.GetAppointments(filter, date, cancellationToken);
        _cacheService.Set(key, result);
        var keys = _cacheService.Get<HashSet<string>>(APPOINTMENT);
        if (keys != null) {
            keys.Add(key);
            _cacheService.Remove(APPOINTMENT);
            _cacheService.Set(APPOINTMENT, keys);
        }
        else
        {
            HashSet<string> list = new HashSet<string> { key };
            _cacheService.Set(APPOINTMENT, list);
        }
        return result;
    }

    //[HttpPost("schedule")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    //[OpenApiOperation("Schedule Appointment", "")]
    //public Task<string> ScheduleAppointment(ScheduleAppointmentRequest request)
    //{
    //    return Mediator.Send(request);
    //}
    [HttpGet("get/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("Get Appointment by ID", "")]
    public Task<AppointmentResponse> GetAppointmentByID(Guid id, CancellationToken cancellationToken)
    {
        return _appointmentService.GetAppointmentByID(id, cancellationToken);
    }

    [HttpGet("examination/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Toggle Appointment Status, Use for Doctor Click and verify patient who came to clinic", "")]
    public Task<List<TreatmentPlanResponse>> VerifyAppointment(Guid id, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return _appointmentService.ToggleAppointment(id, cancellationToken);
    }

    [HttpGet("followup/checkin/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Toggle Follow up Appointment Status. Use CalendarID", "")]
    public Task<string> VerifyFollowUpAppointment(Guid id, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return _appointmentService.ToggleFollowAppointment(id, cancellationToken);
    }

    [HttpGet("payment/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Get Remaining Amount Of Appointment By AppointmentID", "")]
    public Task<PaymentDetailResponse> GetRemainingAmountOfAppointment(Guid id, CancellationToken cancellationToken)
    {
        return _appointmentService.GetRemainingAmountOfAppointment(id, cancellationToken);
    }

    [HttpPost("payment/do")]
    [OpenApiOperation("Send request for payment method", "")]
    public Task<string> PayForAppointment(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return Mediator.Send(request);
    }

    [HttpDelete("payment/cancel/{code}")]
    [OpenApiOperation("Cancel request for payment", "")]
    public Task<string> CancelPaymentForAppointment(string code, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return _appointmentService.CancelPayment(code, cancellationToken);
    }

    [HttpPost("non-doctor/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Appointments that have non-doctor", "")]
    public async Task<PaginationResponse<AppointmentResponse>> GetNonDoctorAppointments(PaginationFilter filter, [FromQuery] DateOnly date, TimeSpan time, CancellationToken cancellationToken)
    {
        string key = RedisKeyGenerator.GenerateAppointmentKey(
        _currentUserService.GetUserId().ToString(),
        filter,
        date,
        NON
    );
        var r = _cacheService.Get<PaginationResponse<AppointmentResponse>>(key);
        if (r != null)
        {
            return r;
        }
        var result = await _appointmentService.GetNonDoctorAppointments(filter, date, time, cancellationToken);
        _cacheService.Set(key, result);
        var keys = _cacheService.Get<HashSet<string>>(APPOINTMENT);
        if (keys != null)
        {
            keys.Add(key);
            _cacheService.Remove(APPOINTMENT);
            _cacheService.Set(APPOINTMENT, keys);
        }
        else
        {
            HashSet<string> list = new HashSet<string> { key };
            _cacheService.Set(APPOINTMENT, list);
        }
        return result;
    }

    [HttpPost("non-doctor/add-doctor")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("Add Doctor to Appointments that have non-doctor", "")]
    public Task<string> AddDoctorToAppointments(AddDoctorToAppointment request, CancellationToken cancellationToken)
    {
        DeleteRedisCode();
        return _appointmentService.AddDoctorToAppointments(request, cancellationToken);
    }

    //checked
    [HttpPost("follow/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Follow up Appointments", "")]
    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetFollowUpAppointments(PaginationFilter filter, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        string key = RedisKeyGenerator.GenerateAppointmentKey(
            _currentUserService.GetUserId().ToString(),
            filter,
            date,
            FOLLOW
        );
        var r = _cacheService.Get<PaginationResponse<GetWorkingDetailResponse>>(key);
        if (r != null)
        {
            return r;
        }
        var result = await _appointmentService.GetFollowUpAppointments(filter, date, cancellationToken);
        _cacheService.Set(key, result);
        var keys = _cacheService.Get<HashSet<string>>(APPOINTMENT);
        if (keys != null)
        {
            keys.Add(key);
            _cacheService.Remove(APPOINTMENT);
            _cacheService.Set(APPOINTMENT, keys);
        }
        else
        {
            HashSet<string> list = new HashSet<string> { key };
            _cacheService.Set(APPOINTMENT, list);
        }
        return result;
    }

    //checked
    [HttpPost("re/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Re Examination Appointments", "")]
    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetReExamAppointments(PaginationFilter filter, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        string key = RedisKeyGenerator.GenerateAppointmentKey(
            _currentUserService.GetUserId().ToString(),
            filter,
            date,
            REEXAM
        );
        var r = _cacheService.Get<PaginationResponse<GetWorkingDetailResponse>>(key);
        if (r != null)
        {
            return r;
        }
        var result = await _appointmentService.GetReExamAppointments(filter, date, cancellationToken);
        _cacheService.Set(key, result);
        var keys = _cacheService.Get<HashSet<string>>(APPOINTMENT);
        if (keys != null)
        {
            keys.Add(key);
            _cacheService.Remove(APPOINTMENT);
            _cacheService.Set(APPOINTMENT, keys);
        }
        else
        {
            HashSet<string> list = new HashSet<string> { key };
            _cacheService.Set(APPOINTMENT, list);
        }
        return result;
    }

    [HttpGet("cache/delete")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Delete cache", "")]
    public Task<string> DeleteCache()
    {
        DeleteRedisCode();
        return Task.FromResult("Success");
    }

    public Task DeleteRedisCode()
    {
        try
        {
            var keys = _cacheService.Get<HashSet<string>>(APPOINTMENT);
            if(keys != null)
            {
                foreach (string key in keys)
                {
                    _cacheService.Remove(key);
                }
                _cacheService.Remove(APPOINTMENT);
            }
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }
}
