using FSH.WebApi.Application.Appointments;
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

    [HttpPost("verify")]
    [MustHavePermission(FSHAction.Create, FSHResource.Appointment)]
    [OpenApiOperation("Finish Deposit Appointment Payment", "")]
    public Task<string> VerifyAppointment(AppointmentDepositRequest request)
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
}
