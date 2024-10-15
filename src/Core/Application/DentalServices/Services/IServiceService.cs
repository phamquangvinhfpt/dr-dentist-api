using FSH.WebApi.Application.DentalServices.Procedures;
using FSH.WebApi.Domain.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Services;
public interface IServiceService : ITransientService
{
    Task<PaginationResponse<Service>> GetServicesPaginationAsync(PaginationFilter filter, CancellationToken cancellation);
    Task CreateOrUpdateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken);
    Task<string> DeleteServiceAsync(Guid serviceID, CancellationToken cancellationToken);
    Task<ServiceDTO> GetServiceByID(Guid serviceID, CancellationToken cancellationToken);
    Task CreateOrUpdateProcedureAsync(CreateOrUpdateProcedure request, CancellationToken cancellationToken);
    Task<PaginationResponse<Procedure>> GetProcedurePaginationAsync(PaginationFilter request, CancellationToken cancellationToken);
    Task<List<Procedure>> GetProceduresAsync(CancellationToken cancellationToken);
    Task<List<Service>> GetServicesAsync(CancellationToken cancellationToken);

    Task<Procedure> GetProcedureByID(Guid procedureID, CancellationToken cancellationToken);
}
