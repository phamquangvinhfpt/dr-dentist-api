using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices;
public class AddStaffForContactRequest : IRequest<string>
{
    public string? StaffId { get; set; }
    public Guid ContactID { get; set; }
}

public class AddStaffForContactRequestValidator : CustomValidator<AddStaffForContactRequest>
{
    public AddStaffForContactRequestValidator(IUserService userService, ICustomerInformationService customerInformationService)
    {
        RuleFor(p => p.StaffId)
            .NotNull()
            .MustAsync(async (id, _) => await userService.ExistsWithUserIDAsync(id))
            .WithMessage((_, id) => $"User {id} is not existing.");

        RuleFor(p => p.ContactID).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MustAsync(async (id, _) => await customerInformationService.CheckContactExist(id))
            .WithMessage((_, id) => $"Contact {id} is not valid.");

    }
}

public class AddStaffForContactRequestHandler : IRequestHandler<AddStaffForContactRequest, string>
{
    private readonly ICustomerInformationService _customerInformationService;
    private readonly IStringLocalizer<AddStaffForContactRequest> _t;

    public AddStaffForContactRequestHandler(ICustomerInformationService customerInformationService, IStringLocalizer<AddStaffForContactRequest> t)
    {
        _customerInformationService = customerInformationService;
        _t = t;
    }

    public async Task<string> Handle(AddStaffForContactRequest request, CancellationToken cancellationToken)
    {
        await _customerInformationService.AddStaffForContact(request, cancellationToken);
        return _t["Successfully."];
    }
}
