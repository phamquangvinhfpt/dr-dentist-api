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

    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Appointment)]
    [OpenApiOperation("Create Appointment", "")]
    public Task<AppointmentDepositRequest> CreateMedicalHistory(CreateAppointmentRequest request)
    {
        return Mediator.Send(request);
    }
}
