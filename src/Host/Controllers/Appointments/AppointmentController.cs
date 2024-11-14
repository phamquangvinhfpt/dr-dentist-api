using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Payments;
using FSH.WebApi.Application.TreatmentPlan;
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
    public Task<PayAppointmentRequest> CreateAppointment(CreateAppointmentRequest request)
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
    [HttpGet("get/{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.Appointment)]
    [OpenApiOperation("Get Appointment by ID", "")]
    public Task<AppointmentResponse> ScheduleAppointment(Guid id, CancellationToken cancellationToken)
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

    [HttpPost("payment/cancel")]
    [OpenApiOperation("Cancel request for payment", "")]
    public async Task<string> CancelPaymentForAppointment(PayAppointmentRequest request, CancellationToken cancellationToken)
    {
        return await _appointmentService.CancelPayment(request, cancellationToken);
    }
}
