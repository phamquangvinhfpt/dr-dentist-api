using FSH.WebApi.Application.DentalServices.Procedures;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Domain.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.DentalService;
public class ServiceController : VersionNeutralApiController
{
    private readonly IServiceService _serviceService;

    public ServiceController(IServiceService serviceService)
    {
        _serviceService = serviceService;
    }
    [HttpPost("get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get Services.", "")]
    public async Task<PaginationResponse<Service>> CreateServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetServicesPaginationAsync(request, cancellationToken);
    }
    [HttpGet("{id}/get")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get Service Detail.", "")]
    public async Task<ServiceDTO> CreateServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.GetServiceByID(id, cancellationToken);
    }

    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Service)]
    [OpenApiOperation("Create Service.", "")]
    public Task<string> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpPost("update")]
    [MustHavePermission(FSHAction.Update, FSHResource.Service)]
    [OpenApiOperation("Update Service.", "")]
    public Task<string> UpdateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpPost("create-procedure")]
    [MustHavePermission(FSHAction.Create, FSHResource.Procedure)]
    [OpenApiOperation("Create Procedure.", "")]
    public Task<string> CreateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpPost("update-procedure")]
    [MustHavePermission(FSHAction.Update, FSHResource.Procedure)]
    [OpenApiOperation("Create Procedure.", "")]
    public Task<string> UpdateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpDelete("{id}/delete")]
    [MustHavePermission(FSHAction.Delete, FSHResource.Service)]
    [OpenApiOperation("Update Service.", "")]
    public async Task<string> UpdateServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.DeleteServiceAsync(id, cancellationToken);
    }
}
