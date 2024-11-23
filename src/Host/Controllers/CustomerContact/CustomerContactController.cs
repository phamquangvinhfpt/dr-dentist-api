using FSH.WebApi.Application.CustomerServices;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSH.WebApi.Host.Controllers.CustomerContact;

public class CustomerContactController : VersionNeutralApiController
{
    private readonly ICustomerInformationService _customerInformationService;

    public CustomerContactController(ICustomerInformationService customerInformationService)
    {
        _customerInformationService = customerInformationService;
    }
    [HttpPost("get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.ContactInformation)]
    [OpenApiOperation("Get all Contact request with pagination has staff.", "")]
    public async Task<PaginationResponse<ContactResponse>> GetAllServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _customerInformationService.GetAllContactRequest(request, cancellationToken);
    }
    [HttpPost("non-staff/get-all")]
    [MustHavePermission(FSHAction.View, FSHResource.ContactInformation)]
    [OpenApiOperation("Get all Contact request with pagination that do not have staff.", "")]
    public async Task<PaginationResponse<ContactResponse>> NonStaffGetAllServiceAsync(PaginationFilter request, CancellationToken cancellationToken)
    {
        return await _customerInformationService.GetAllContactRequestNonStaff(request, cancellationToken);
    }
    [HttpPost("add")]
    [TenantIdHeader]
    [AllowAnonymous]
    [OpenApiOperation("Customer send contact request.", "")]
    public Task<string> AddContactInformationAsync(ContactInformationRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }
    [HttpPost("add-staff")]
    [MustHavePermission(FSHAction.Update, FSHResource.ContactInformation)]
    [OpenApiOperation("Staff get information and contact with guest.", "")]
    public Task<string> AddStaffToContactAsync(AddStaffForContactRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }
}
