using FSH.WebApi.Application.Identity.Users;
using FSH.WebApi.Shared.Authorization;

namespace FSH.WebApi.Application.Identity.Roles;

public class UpdateRolePermissionsRequest : IRequest<string>
{
    public string UserID { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string Resource { get; set; } = default!;
}

public class UpdateRolePermissionsRequestValidator : CustomValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(r => r.UserID)
            .NotEmpty();
        RuleFor(r => r.Action)
            .NotNull()
            .Must(p => p == FSHAction.View || p == FSHAction.Update || p == FSHAction.Create || p == FSHAction.Delete)
            .WithErrorCode("113").WithMessage("Khong duoc dau be oi.");
        RuleFor(r => r.Resource)
            .NotEmpty()
            .Must(p => p == FSHResource.Appointment || p == FSHResource.MedicalHistory || p == FSHResource.GeneralExamination || p == FSHResource.Users)
            .WithErrorCode("113").WithMessage("Resource sai roi ne be oi.");
    }
}
public class UpdateRolePermissionsRequestHandler : IRequestHandler<UpdateRolePermissionsRequest, string>
{
    private readonly IRoleService _roleService;
    private readonly IStringLocalizer<UpdateRolePermissionsRequest> _t;

    public UpdateRolePermissionsRequestHandler(IRoleService roleService, IStringLocalizer<UpdateRolePermissionsRequest> t)
    {
        _roleService = roleService;
        _t = t;
    }

    public async Task<string> Handle(UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        await _roleService.AssignPermissionsAsync(request, cancellationToken);
        return _t["Permission updated successfully."];
    }
}
