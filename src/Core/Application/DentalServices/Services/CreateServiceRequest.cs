using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Services;
public class CreateServiceRequest : IRequest<string>
{
    public Guid ServiceID { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid TypeID { get; set; }
    public bool IsModify { get; set; } = false;
}

public class CreateServiceRequestValidator : CustomValidator<CreateServiceRequest>
{
    public CreateServiceRequestValidator()
    {
        RuleFor(p => p.ServiceID)
            .NotEmpty()
            .When(b => b.IsModify)
            .WithMessage("Name should not be empty.");

        RuleFor(p => p.Name)
            .NotEmpty()
            .WithMessage("Name should not be empty.");
        RuleFor(p => p.Description)
            .NotEmpty()
            .WithMessage("Description should not be empty.");
    }
}

public class CreateServiceRequestHandler : IRequestHandler<CreateServiceRequest, string>
{
    private readonly IServiceService _serviceService;
    private readonly IStringLocalizer<CreateServiceRequestHandler> _t;

    public CreateServiceRequestHandler(IServiceService serviceService, IStringLocalizer<CreateServiceRequestHandler> t)
    {
        _serviceService = serviceService;
        _t = t;
    }

    public async Task<string> Handle(CreateServiceRequest request, CancellationToken cancellationToken)
    {
        if (request.IsModify)
        {
            await _serviceService.ModifyServiceAsync(request, cancellationToken);
        }
        else {
            await _serviceService.CreateServiceAsync(request, cancellationToken);
        }
        return _t["Update Service Sucsess"];
    }
}
