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
}
