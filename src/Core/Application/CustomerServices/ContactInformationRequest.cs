using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.CustomerServices;
public class ContactInformationRequest : IRequest<string>
{
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
public class ContactInformationRequestValidator : CustomValidator<ContactInformationRequest>
{
    public ContactInformationRequestValidator()
    {
        RuleFor(u => u.Email).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .EmailAddress()
                .WithMessage("Invalid Email Address.");

        RuleFor(u => u.Phone).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phone number is required.")
            .Matches(@"^(0|84|\\+84)(3|5|7|8|9)[0-9]{8}$")
            .WithMessage("Invalid phone number format. Please enter a valid Vietnamese phone number.");

        RuleFor(p => p.Content).Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Content should not empty.");
    }
}

public class ContactInformationRequestHandler : IRequestHandler<ContactInformationRequest, string>
{
    private readonly ICustomerInformationService _customerInformationService;
    private readonly IStringLocalizer<ContactInformationRequest> _t;

    public ContactInformationRequestHandler(ICustomerInformationService customerInformationService, IStringLocalizer<ContactInformationRequest> t)
    {
        _customerInformationService = customerInformationService;
        _t = t;
    }

    public async Task<string> Handle(ContactInformationRequest request, CancellationToken cancellationToken)
    {
        await _customerInformationService.AddCustomerInformation(request);
        return _t["Send Successfully. Staff will call you soon. Thank for using our service"];
    }
}