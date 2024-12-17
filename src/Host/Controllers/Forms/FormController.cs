using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Application.Common.Caching;
using FSH.WebApi.Application.Identity.ApplicationForms;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.Forms;
public class FormController : VersionedApiController
{
    private readonly IApplicationFormService _applicationFormService;

    public FormController(IApplicationFormService applicationFormService)
    {
        _applicationFormService = applicationFormService;
    }

    [HttpPost("get-all")]
    [OpenApiOperation("View Forms", "")]
    public async Task<PaginationResponse<FormDetailResponse>> GetAppointments(PaginationFilter filter, CancellationToken cancellationToken)
    {
        return await _applicationFormService.GetFormDetails(filter, cancellationToken);
    }

    [HttpPost("add")]
    [OpenApiOperation("Add Forms", "")]
    public async Task<string> AddFormAsync(AddFormRequest filter, CancellationToken cancellationToken)
    {
        return await _applicationFormService.AddFormAsync(filter, cancellationToken);
    }

    [HttpPost("toggle")]
    [OpenApiOperation("Toggle Forms", "")]
    public async Task<string> AddFormAsync(ToggleFormRequest filter, CancellationToken cancellationToken)
    {
        return await _applicationFormService.ToggleFormAsync(filter, cancellationToken);
    }
}
