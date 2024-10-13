using FluentValidation.Results;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices.Procedures;
public class CreateOrUpdateProcedure : IRequest<string>
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal Price { get; set; }
    public bool isDuplicate { get; set; } = false;
    public bool hasService { get; set; } = false;
    public Guid ServiceID { get; set; }
}

public class CreateOrUpdateProcedureValidator : CustomValidator<CreateOrUpdateProcedure>
{
    public CreateOrUpdateProcedureValidator()
    {
        RuleFor(p => p)
            .CustomAsync(async (profile, context, cancellationToken) =>
            {
                // For Duplicate
                if (profile.isDuplicate)
                {
                    if (profile.hasService)
                    {
                        if (profile.ServiceID == Guid.Empty)
                        {
                            context.AddFailure(new ValidationFailure(string.Empty,
                                "The service is empty. Procedure can not duplicate")
                            {
                                ErrorCode = "BadRequest"
                            });
                        }
                        if (profile.Id == Guid.Empty)
                        {
                            context.AddFailure(new ValidationFailure(string.Empty,
                                "The procedure is empty. Procedure can not update")
                            {
                                ErrorCode = "BadRequest"
                            });
                        }
                    }

                }
            });

        RuleFor(p => p.Name)
            .NotEmpty()
            .WithMessage("Name should not be empty.");

        RuleFor(p => p.Description)
            .NotEmpty()
            .WithMessage("Description should not be empty.");
        RuleFor(p => p.Price)
            .NotEmpty()
            .WithMessage("Price should not be empty.")
            .Must(p => p > 0)
            .WithMessage("Price must be valid.");
    }
}
public class CreateOrUpdateProcedureHandler : IRequestHandler<CreateOrUpdateProcedure, string>
{
    private readonly IServiceService _serviceService;
    private readonly IStringLocalizer<CreateOrUpdateProcedureHandler> _t;

    public CreateOrUpdateProcedureHandler(IServiceService serviceService, IStringLocalizer<CreateOrUpdateProcedureHandler> t)
    {
        _serviceService = serviceService;
        _t = t;
    }

    public async Task<string> Handle(CreateOrUpdateProcedure request, CancellationToken cancellationToken)
    {
        await _serviceService.CreateOrUpdateProcedureAsync(request, cancellationToken);
        return _t["Update Service Sucsess"];
    }
}