﻿using FSH.WebApi.Application.DentalServices.Services;
using FSH.WebApi.Application.Identity.MedicalHistories;
using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Application.DentalServices;
public class AddOrDeleteProcedureToService : IRequest<string>
{
    public Guid ServiceID { get; set; }
    public Guid ProcedureID { get; set; }
    public bool IsRemove { get; set; } = false;
}
public class AddOrDeleteProcedureToServiceValidator : CustomValidator<AddOrDeleteProcedureToService>
{
    public AddOrDeleteProcedureToServiceValidator(IServiceService serviceService)
    {
        RuleFor(p => p.ServiceID)
            .NotNull()
            .MustAsync(async (id, _) => await serviceService.CheckExistingService(id))
            .WithMessage((_, id) => $"Service {id} is not existed or deactivated.");
        RuleFor(p => p.ProcedureID)
            .NotNull()
            .MustAsync(async (id, _) => await serviceService.CheckExistingProcedure(id))
            .WithMessage((_, id) => $"Procedure {id} is not existed or deleted.");
    }
}

public class AddOrDeleteProcedureToServiceHandler : IRequestHandler<AddOrDeleteProcedureToService, string>
{
    private readonly IServiceService _serviceService;
    private readonly IStringLocalizer<AddOrDeleteProcedureToService> _t;

    public AddOrDeleteProcedureToServiceHandler(IStringLocalizer<AddOrDeleteProcedureToService> t, IServiceService serviceService)
    {
        _serviceService = serviceService;
        _t = t;
    }

    public async Task<string> Handle(AddOrDeleteProcedureToService request, CancellationToken cancellationToken)
    {
        await _serviceService.AddOrDeleteProcedureToService(request, cancellationToken);
        return _t["Successfully."];
    }
}