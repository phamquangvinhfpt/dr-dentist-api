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
    [HttpPost("pagination/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get Services with pagination.", "")]
    public async Task<PaginationResponse<Service>> GetAllServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetServicesPaginationAsync(request, cancellationToken);
    }
    [HttpGet("{id}/get")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get Service Detail.", "")]
    public async Task<ServiceDTO> GetServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.GetServiceByID(id, cancellationToken);
    }

    [HttpGet("{id}/delete")]
    [MustHavePermission(FSHAction.Delete, FSHResource.Service)]
    [OpenApiOperation("Delete Service.", "")]
    public async Task<string> DeleteServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.DeleteServiceAsync(id, cancellationToken);
    }

    [HttpGet("/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get all Service.", "")]
    public async Task<List<Service>> GetServicesAsync(CancellationToken cancellationToken)
    {
        return await _serviceService.GetServicesAsync(cancellationToken);
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

    [HttpPost("procedure/create-procedure")]
    [MustHavePermission(FSHAction.Create, FSHResource.Procedure)]
    [OpenApiOperation("Create Procedure.", "")]
    public Task<string> CreateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpPost("procedure/update-procedure")]
    [MustHavePermission(FSHAction.Update, FSHResource.Procedure)]
    [OpenApiOperation("Update Procedure.", "")]
    public Task<string> UpdateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpPost("procedure/pagination/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Procedure)]
    [OpenApiOperation("Get Procedures with pagination.", "")]
    public async Task<PaginationResponse<Procedure>> GetAllProcedureAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetProcedurePaginationAsync(request, cancellationToken);
    }
    [HttpGet("procedure/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Procedure)]
    [OpenApiOperation("Get all list procedure.", "")]
    public async Task<List<Procedure>> GetProceduresAsync(CancellationToken cancellationToken)
    {
        return await _serviceService.GetProceduresAsync(cancellationToken);
    }

    [HttpGet("procedure/{id}/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Procedure)]
    [OpenApiOperation("Get procedure By ID.", "")]
    public async Task<Procedure> GetProcedureByIDAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.GetProcedureByID(id,cancellationToken);
    }
}
