using FSH.WebApi.Application.DentalServices.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices;

public class AddTypeServiceRequest : IRequest<string>
{
    public string? TypeName { get; set; }
    public string? TypeDescription { get; set; }
}

public class AddTypeServiceRequestValidator : CustomValidator<AddTypeServiceRequest>
{
    public AddTypeServiceRequestValidator()
    {
        RuleFor(p => p.TypeName)
            .NotNull()
            .WithMessage("Name can not be null");
        RuleFor(p => p.TypeDescription)
            .NotNull()
            .WithMessage("Description can not be null");
    }
}


public class AddTypeServiceRequestHandler : IRequestHandler<AddTypeServiceRequest, string>
{
    private readonly IServiceService _serviceService;

    public AddTypeServiceRequestHandler(IServiceService serviceService)
    {
        _serviceService = serviceService;
    }

    public async Task<string> Handle(AddTypeServiceRequest request, CancellationToken cancellationToken)
    {
        return await _serviceService.AddTypeServiceAsync(request, cancellationToken);
    }
}