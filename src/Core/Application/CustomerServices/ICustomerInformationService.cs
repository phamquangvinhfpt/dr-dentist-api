using FSH.WebApi.Domain.CustomerServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices;
public interface ICustomerInformationService : ITransientService
{
    Task<bool> CheckContactExist(Guid id);
    Task AddCustomerInformation(ContactInformationRequest request);
    Task<PaginationResponse<ContactResponse>> GetAllContactRequest(PaginationFilter request, CancellationToken cancellationToken);
    Task AddStaffForContact(AddStaffForContactRequest request, CancellationToken cancellationToken);
    Task<PaginationResponse<ContactResponse>> GetAllContactRequestNonStaff(PaginationFilter request, CancellationToken cancellationToken);
    Task<PaginationResponse<ContactResponse>> GetAllContactRequestForStaff(PaginationFilter request, CancellationToken cancellationToken);
}
