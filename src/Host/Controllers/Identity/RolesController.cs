using FSH.WebApi.Application.Identity.Roles;

namespace FSH.WebApi.Host.Controllers.Identity;

public class RolesController : VersionNeutralApiController
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpGet]
    [MustHavePermission(FSHAction.View, FSHResource.Roles)]
    [OpenApiOperation("Get a list of all roles.", "")]
    public Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken)
    {
        return _roleService.GetListAsync(cancellationToken);
    }

    [HttpGet("{id}")]
    [MustHavePermission(FSHAction.View, FSHResource.Roles)]
    [OpenApiOperation("Get role details.", "")]
    public Task<RoleDto> GetByIdAsync(string id)
    {
        return _roleService.GetByIdAsync(id);
    }

    //[HttpGet("{id}/permissions")]
    //[MustHavePermission(FSHAction.View, FSHResource.RoleClaims)]
    //[OpenApiOperation("Get role details with its permissions.", "")]
    //public Task<RoleDto> GetByIdWithPermissionsAsync(string id, CancellationToken cancellationToken)
    //{
    //    return _roleService.GetByIdWithPermissionsAsync(id, cancellationToken);
    //}
    [HttpGet("{id}/permissions")]
    [MustHavePermission(FSHAction.View, FSHResource.RoleClaims)]
    [OpenApiOperation("Get permissions of Staff or Doctor by using userID.", "")]
    public Task<List<string>> GetByIdWithPermissionsAsync(string id, CancellationToken cancellationToken)
    {
        return _roleService.GetUserPermissionByUserID(id, cancellationToken);
    }
    [HttpGet("/permissions")]
    [MustHavePermission(FSHAction.View, FSHResource.RoleClaims)]
    [OpenApiOperation("Get all role with its permissions.", "")]
    public Task<List<RoleDto>> GetAllRoleWithPermissionsAsync(CancellationToken cancellationToken)
    {
        return _roleService.GetListWithPermissionAsync(cancellationToken);
    }
    [HttpPut("update/permissions")]
    [MustHavePermission(FSHAction.Update, FSHResource.RoleClaims)]
    [OpenApiOperation("Update a role's permissions.", "")]
    public Task<string> UpdatePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        return Mediator.Send(request);
    }
    [HttpPut("delete/permissions")]
    [MustHavePermission(FSHAction.Update, FSHResource.RoleClaims)]
    [OpenApiOperation("Delete a role's permissions.", "")]
    public async Task<ActionResult<string>> DeletePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _roleService.DeletePermissionsAsync(request, cancellationToken));
    }
    [HttpPost]
    [MustHavePermission(FSHAction.Create, FSHResource.Roles)]
    [OpenApiOperation("Create or update a role.", "")]
    public Task<string> RegisterRoleAsync(CreateOrUpdateRoleRequest request)
    {
        return _roleService.CreateOrUpdateAsync(request);
    }

    [HttpDelete("{id}")]
    [MustHavePermission(FSHAction.Delete, FSHResource.Roles)]
    [OpenApiOperation("Delete a role.", "")]
    public Task<string> DeleteAsync(string id)
    {
        return _roleService.DeleteAsync(id);
    }
}