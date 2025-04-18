﻿using FSH.WebApi.Application.Appointments;
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
    // private static string APPOINTMENT = "APPOINTMENT";
    // private static string FOLLOW = "FOLLOW";
    // private static string REEXAM = "REEXAM";
    // private static string NON = "NON";
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
        return Mediator.Send(request);
    }
    //checked
    [HttpPost("re/create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Appointment)]
    [OpenApiOperation("Create ReExamination Appointment", "")]
    public Task<string> CreateReAppointment(AddReExamination request, CancellationToken cancellationToken)
    {
        return _appointmentService.CreateReExamination(request, cancellationToken);
    }

    //checked
    [HttpPost("doctor/available")]
    [OpenApiOperation("Get Doctors Available for service at Date and Time", "")]
    public async Task<List<GetDoctorResponse>> GetAvailableDoctor(GetAvailableDoctor request, CancellationToken cancellationToken)
    {
        return await _appointmentService.GetAvailableDoctorAsync(request, cancellationToken);
    }

    [HttpPost("cancel")]
    [MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Cancel Appointment", "")]
    public Task<string> CancelAppointment(CancelAppointmentRequest request)
    {
        return Mediator.Send(request);
    }

    [HttpPost("reschedule")]
    [MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Reschedule Appointment", "")]
    public Task<string> RescheduleAppointment(RescheduleRequest request)
    {
        return Mediator.Send(request);
    }
    //checked
    [HttpPost("get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Appointments", "")]
    public async Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
    //     string key = RedisKeyGenerator.GenerateAppointmentKey(
    //     _currentUserService.GetUserId().ToString(),
    //     filter,
    //     date,
    //     default,
    //     APPOINTMENT
    // );
    //     var r = _cacheService.Get<PaginationResponse<AppointmentResponse>>(key);
    //     if (r != null) {
    //         return r;
    //     }
    //     var result = await _appointmentService.GetAppointments(filter, date, cancellationToken);
    //     _cacheService.Set(key, result);
    //     var keys = _cacheService.Get<HashSet<string>>(APPOINTMENT);
    //     if (keys != null) {
    //         keys.Add(key);
    //         _cacheService.Remove(APPOINTMENT);
    //         _cacheService.Set(APPOINTMENT, keys);
    //     }
    //     else
    //     {
    //         HashSet<string> list = new HashSet<string> { key };
    //         _cacheService.Set(APPOINTMENT, list);
    //     }
        return await _appointmentService.GetAppointments(filter, date, cancellationToken);
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
        return _appointmentService.ToggleAppointment(id, cancellationToken);
    }

    [HttpGet("followup/checkin/{id}")]
    //[MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Toggle Follow up Appointment Status. Use CalendarID", "")]
    public Task<string> VerifyFollowUpAppointment(Guid id, CancellationToken cancellationToken)
    {
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
        return Mediator.Send(request);
    }

    [HttpDelete("payment/cancel/{code}")]
    [OpenApiOperation("Cancel request for payment", "")]
    public Task<string> CancelPaymentForAppointment(string code, CancellationToken cancellationToken)
    {
        return _appointmentService.CancelPayment(code, cancellationToken);
    }

    [HttpPost("non-doctor/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Appointments that have non-doctor", "")]
    public async Task<PaginationResponse<AppointmentResponse>> GetNonDoctorAppointments(PaginationFilter filter, [FromQuery] DateOnly date, TimeSpan time, CancellationToken cancellationToken)
    {
    //     string key = RedisKeyGenerator.GenerateAppointmentKey(
    //     _currentUserService.GetUserId().ToString(),
    //     filter,
    //     date,
    //     time,
    //     NON
    // );
    //     var r = _cacheService.Get<PaginationResponse<AppointmentResponse>>(key);
    //     if (r != null)
    //     {
    //         return r;
    //     }
    //     var result = await _appointmentService.GetNonDoctorAppointments(filter, date, time, cancellationToken);
    //     _cacheService.Set(key, result);
    //     var keys = _cacheService.Get<HashSet<string>>(NON);
    //     if (keys != null)
    //     {
    //         keys.Add(key);
    //         _cacheService.Remove(NON);
    //         _cacheService.Set(NON, keys);
    //     }
    //     else
    //     {
    //         HashSet<string> list = new HashSet<string> { key };
    //         _cacheService.Set(NON, list);
    //     }
        return await _appointmentService.GetNonDoctorAppointments(filter, date, time, cancellationToken);
    }

    [HttpPost("non-doctor/add-doctor")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("Add Doctor to Appointments that have non-doctor", "")]
    public Task<string> AddDoctorToAppointments(AddDoctorToAppointment request, CancellationToken cancellationToken)
    {
        return _appointmentService.AddDoctorToAppointments(request, cancellationToken);
    }

    //checked
    [HttpPost("follow/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Follow up Appointments", "")]
    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetFollowUpAppointments(PaginationFilter filter, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        // string key = RedisKeyGenerator.GenerateAppointmentKey(
        //     _currentUserService.GetUserId().ToString(),
        //     filter,
        //     date,
        //     default,
        //     FOLLOW
        // );
        // var r = _cacheService.Get<PaginationResponse<GetWorkingDetailResponse>>(key);
        // if (r != null)
        // {
        //     return r;
        // }
        // var result = await _appointmentService.GetFollowUpAppointments(filter, date, cancellationToken);
        // _cacheService.Set(key, result);
        // var keys = _cacheService.Get<HashSet<string>>(FOLLOW);
        // if (keys != null)
        // {
        //     keys.Add(key);
        //     _cacheService.Remove(FOLLOW);
        //     _cacheService.Set(FOLLOW, keys);
        // }
        // else
        // {
        //     HashSet<string> list = new HashSet<string> { key };
        //     _cacheService.Set(FOLLOW, list);
        // }
        return await _appointmentService.GetFollowUpAppointments(filter, date, cancellationToken);
    }

    //checked
    [HttpPost("re/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("View Re Examination Appointments", "")]
    public async Task<PaginationResponse<GetWorkingDetailResponse>> GetReExamAppointments(PaginationFilter filter, [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        // string key = RedisKeyGenerator.GenerateAppointmentKey(
        //     _currentUserService.GetUserId().ToString(),
        //     filter,
        //     date,
        //     default,
        //     REEXAM
        // );
        // var r = _cacheService.Get<PaginationResponse<GetWorkingDetailResponse>>(key);
        // if (r != null)
        // {
        //     return r;
        // }
        // var result = await _appointmentService.GetReExamAppointments(filter, date, cancellationToken);
        // _cacheService.Set(key, result);
        // var keys = _cacheService.Get<HashSet<string>>(REEXAM);
        // if (keys != null)
        // {
        //     keys.Add(key);
        //     _cacheService.Remove(REEXAM);
        //     _cacheService.Set(REEXAM, keys);
        // }
        // else
        // {
        //     HashSet<string> list = new HashSet<string> { key };
        //     _cacheService.Set(REEXAM, list);
        // }
        return await _appointmentService.GetReExamAppointments(filter, date, cancellationToken);
    }

    [HttpGet("cache/delete")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Delete cache", "")]
    public Task<string> DeleteCache()
    {
        _appointmentService.DeleteRedisCode();
        return Task.FromResult("Success");
    }

    [HttpGet("date/test")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Test convert date", "")]
    public Task<string> convertdate([FromQuery] DateOnly date)
    {
        string formattedDate = date.ToString("dd-MM-yyyy");
        return Task.FromResult(formattedDate);
    }

    [HttpGet("payment/revert/{id}")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Revert Payment", "")]
    public Task<string> RevertPayment(Guid id)
    {
        _appointmentService.DeleteRedisCode();
        return _appointmentService.RevertPayment(id);
    }
    [HttpGet("job")]
    [AllowAnonymous]
    [TenantIdHeader]
    [OpenApiOperation("Appointment Job", "")]
    public Task T()
    {
        return _appointmentService.JobAppointmentsAsync();
    }
    //[HttpGet("followup/job")]
    //[AllowAnonymous]
    //[TenantIdHeader]
    //[OpenApiOperation("Follow Up Appointment Job", "")]
    //public Task T2()
    //{
    //    return _appointmentService.JobFollowAppointmentsAsync();
    //}
}
