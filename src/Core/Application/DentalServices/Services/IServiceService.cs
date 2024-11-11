using FSH.WebApi.Application.DentalServices.Procedures;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Domain.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Services;
public interface IServiceService : ITransientService
{
    Task<bool> CheckExistingService(Guid serviceId);
    Task<bool> CheckExistingProcedure(Guid procedureID);
    Task<PaginationResponse<Service>> GetServicesPaginationAsync(PaginationFilter filter, CancellationToken cancellation);
    Task CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken);
    Task ModifyServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken);
    Task<string> ToggleServiceAsync(ToggleStatusRequest request, CancellationToken cancellationToken);
    Task<ServiceDTO> GetServiceByID(Guid serviceID, CancellationToken cancellationToken);
    Task CreateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken);
    Task ModifyProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken);
    Task<PaginationResponse<Procedure>> GetProcedurePaginationAsync(PaginationFilter request, CancellationToken cancellationToken);
    Task<List<Procedure>> GetProceduresAsync(CancellationToken cancellationToken);
    Task<List<Service>> GetServicesAsync();

    Task<ProcedureDTOs> GetProcedureByID(Guid procedureID, CancellationToken cancellationToken);
    Task<string> DeleteServiceAsync(Guid id, CancellationToken cancellationToken);
    Task<PaginationResponse<Service>> GetDeletedServiceAsync(PaginationFilter request, CancellationToken cancellationToken);
    Task<string> RestoreServiceAsync(Guid id, CancellationToken cancellationToken);
    Task AddOrDeleteProcedureToService(AddOrDeleteProcedureToService request, CancellationToken cancellationToken);
    Task<string> DeleteProcedureAsync(DefaultIdType id, CancellationToken cancellationToken);
    Task<PaginationResponse<Procedure>> GetDeletedProcedureAsync(PaginationFilter request, CancellationToken cancellationToken);
    Task<string> RestoreProcedureAsync(DefaultIdType id, CancellationToken cancellationToken);
    Task<List<ProcedurePlanResponse>> GetProceduresByServiceID(Guid serviceID, CancellationToken cancellationToken);
}
