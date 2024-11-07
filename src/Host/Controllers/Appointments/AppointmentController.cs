﻿using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.MedicalHistories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.Appointments;
public class AppointmentController : VersionNeutralApiController
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }
    //checked
    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Appointment)]
    [OpenApiOperation("Create Appointment", "")]
    public Task<AppointmentDepositRequest> CreateAppointment(CreateAppointmentRequest request)
    {
        return Mediator.Send(request);
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
    public async Task<PaginationResponse<AppointmentResponse>> GetAppointments(PaginationFilter filter, CancellationToken cancellationToken)
    {
        return await _appointmentService.GetAppointments(filter, cancellationToken);
    }

    [HttpPost("schedule")]
    [MustHavePermission(FSHAction.Update, FSHResource.Appointment)]
    [OpenApiOperation("Schedule Appointment", "")]
    public Task<string> ScheduleAppointment(ScheduleAppointmentRequest request)
    {
        return Mediator.Send(request);
    }
}
