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
    public double Price { get; set; }
    public bool isModify { get; set; } = false;
    public bool hasService { get; set; } = false;
    public Guid ServiceID { get; set; }
}

public class CreateOrUpdateProcedureValidator : CustomValidator<CreateOrUpdateProcedure>
{
    public CreateOrUpdateProcedureValidator(IServiceService serviceService)
    {
        RuleFor(p => p.Id)
            .NotEmpty()
            .When(p => p.isModify)
            .WithMessage("Update what procedure ?.")
            .MustAsync(async (id, _) => await serviceService.CheckExistingProcedure(id))
            .When(p => p.isModify)
            .WithMessage("Service is not found.");

        RuleFor(p => p.ServiceID)
            .NotEmpty()
            .When(p => p.isModify && p.hasService)
            .WithMessage("Update for what service ?.")
            .MustAsync(async (id, _) => await serviceService.CheckExistingService(id))
            .When(p => p.isModify && p.hasService)
            .WithMessage("Service is not found.");

        RuleFor(p => p.Name)
            .NotEmpty()
            .WithMessage("Name should not be empty.");

        RuleFor(p => p.Description)
            .NotEmpty()
            .WithMessage("Description should not be empty.");
        RuleFor(p => p.Price)
            .NotEmpty()
            .WithMessage("Price should not be empty.")
            .Must(p => p >= 100000)
            .WithMessage("Price must be greater or equal 100.000.");
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
        if (request.isModify){
            await _serviceService.ModifyProcedureAsync(request, cancellationToken);
        }
        else
        {
            await _serviceService.CreateProcedureAsync(request, cancellationToken);
        }
        return _t["Update Service Sucsess"];
    }
}