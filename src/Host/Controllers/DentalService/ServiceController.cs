using FSH.WebApi.Application.DentalServices;
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
    //checked
    [HttpPost("pagination/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get Services with pagination.", "")]
    public async Task<PaginationResponse<Service>> GetAllServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetServicesPaginationAsync(request, cancellationToken);
    }
    //checked
    [HttpGet("{id}/get")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get Service Detail.", "")]
    public async Task<ServiceDTO> GetServiceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.GetServiceByID(id, cancellationToken);
    }
    //checked
    [HttpPost("toggle")]
    [MustHavePermission(FSHAction.Update, FSHResource.Service)]
    [OpenApiOperation("Toggle Service Status.", "")]
    public async Task<string> DeleteServiceAsync(ToggleStatusRequest request, CancellationToken cancellationToken)
    {
        return await _serviceService.ToggleServiceAsync(request, cancellationToken);
    }
    //checked
    [HttpDelete("{id}/delete")]
    [MustHavePermission(FSHAction.Delete, FSHResource.Service)]
    [OpenApiOperation("Delete Service.", "")]
    public async Task<string> DeleteServicesAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.DeleteServiceAsync(id, cancellationToken);
    }
    //checked
    [HttpPost("bin")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get all service that was deleted.", "")]
    public async Task<PaginationResponse<Service>> GetDeleteServicesAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetDeletedServiceAsync(request, cancellationToken);
    }
    //checked
    [HttpGet("{id}/restore")]
    [MustHavePermission(FSHAction.Update, FSHResource.Service)]
    [OpenApiOperation("Restore service that was deleted.", "")]
    public async Task<string> RestoreServicesAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.RestoreServiceAsync(id, cancellationToken);
    }

    //checked
    [HttpPost("create")]
    [MustHavePermission(FSHAction.Create, FSHResource.Service)]
    [OpenApiOperation("Create Service.", "")]
    public Task<string> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    //checked
    [HttpPost("modify")]
    [MustHavePermission(FSHAction.Update, FSHResource.Service)]
    [OpenApiOperation("Update if service don't have any procedure, otherwise Modify Existing Service and deactivate this service.", "")]
    public Task<string> ModifyServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }
    //checked
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
    //checked
    [HttpPost("procedure/pagination/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.Procedure)]
    [OpenApiOperation("Get Procedures with pagination.", "")]
    public async Task<PaginationResponse<Procedure>> GetAllProcedureAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetProcedurePaginationAsync(request, cancellationToken);
    }
    //checked
    [HttpGet("procedure/get-all")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Get all list procedure.", "")]
    public async Task<List<Procedure>> GetProceduresAsync(CancellationToken cancellationToken)
    {
        return await _serviceService.GetProceduresAsync(cancellationToken);
    }
    //checked
    [HttpGet("procedure/{id}/get")]
    [MustHavePermission(FSHAction.View, FSHResource.Procedure)]
    [OpenApiOperation("Get procedure By ID.", "")]
    public async Task<ProcedureDTOs> GetProcedureByIDAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.GetProcedureByID(id,cancellationToken);
    }

    //checked
    [HttpGet("customer-get-all")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Get all service for customer.", "")]
    public async Task<List<Service>> GetAllServicesAsync()
    {
        return await _serviceService.GetServicesAsync();
    }

    [HttpPost("add-delete-procedure")]
    [MustHavePermission(FSHAction.Update, FSHResource.Service)]
    [OpenApiOperation("Add or Delete Procedure throw Service.", "")]
    public Task<string> AddProcedureToServiceAsync(AddOrDeleteProcedureToService request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }

    [HttpDelete("procedure/{id}/delete")]
    [MustHavePermission(FSHAction.Delete, FSHResource.Procedure)]
    [OpenApiOperation("Delete procedure.", "")]
    public async Task<string> DeleteProcedureAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.DeleteProcedureAsync(id, cancellationToken);
    }
    [HttpPost("procedure/bin")]
    [MustHavePermission(FSHAction.View, FSHResource.Service)]
    [OpenApiOperation("Get all procedure that was deleted.", "")]
    public async Task<PaginationResponse<Procedure>> GetDeleteProceduresAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _serviceService.GetDeletedProcedureAsync(request, cancellationToken);
    }
    //checked
    [HttpGet("procedure/{id}/restore")]
    [MustHavePermission(FSHAction.Update, FSHResource.Procedure)]
    [OpenApiOperation("Restore procedure that was deleted.", "")]
    public async Task<string> RestoreProcedureAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _serviceService.RestoreProcedureAsync(id, cancellationToken);
    }
}
