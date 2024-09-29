namespace FSH.WebApi.Application.Identity.Roles;

public class UpdateRolePermissionsRequest
{
    public string RoleId { get; set; } = default!;
    public string Permissions { get; set; } = default!;
}

public class UpdateRolePermissionsRequestValidator : CustomValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(r => r.RoleId)
            .NotEmpty();
        RuleFor(r => r.Permissions)
            .NotNull();
    }
}