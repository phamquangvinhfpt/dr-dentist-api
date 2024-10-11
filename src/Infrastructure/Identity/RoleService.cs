using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Common.Events;
using FSH.WebApi.Application.Common.Exceptions;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Application.Identity.Roles;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Multitenancy;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.Collections;
using System.Security.Claims;

namespace FSH.WebApi.Infrastructure.Identity;

internal class RoleService : IRoleService
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizer _t;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantInfo _currentTenant;
    private readonly IEventPublisher _events;

    public RoleService(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IStringLocalizer<RoleService> localizer,
        ICurrentUser currentUser,
        ITenantInfo currentTenant,
        IEventPublisher events)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _db = db;
        _t = localizer;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _events = events;
    }

    public async Task<List<RoleDto>> GetListAsync(CancellationToken cancellationToken) =>
        (await _roleManager.Roles.Where(p => p.Name != FSHRoles.Admin).ToListAsync(cancellationToken))
            .Adapt<List<RoleDto>>();

    public async Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        await _roleManager.Roles.CountAsync(cancellationToken);

    public async Task<bool> ExistsAsync(string roleName, string? excludeId) =>
        await _roleManager.FindByNameAsync(roleName)
            is ApplicationRole existingRole
            && existingRole.Id != excludeId;

    public async Task<RoleDto> GetByIdAsync(string id) =>
        await _db.Roles.SingleOrDefaultAsync(x => x.Id == id) is { } role
            ? role.Adapt<RoleDto>()
            : throw new NotFoundException(_t["Role Not Found"]);

    public async Task<RoleDto> GetByIdWithPermissionsAsync(string roleId, CancellationToken cancellationToken)
    {
        var role = await GetByIdAsync(roleId);

        role.Permissions = await _db.RoleClaims
            .Where(c => c.RoleId == roleId && c.ClaimType == FSHClaims.Permission)
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);

        return role;
    }

    public async Task<List<string>> GetUserPermissionByUserID(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId) ?? throw new NotFoundException("User is not found.");
        var roles = await _userManager.GetRolesAsync(user);
        var role = await _roleManager.FindByNameAsync(roles[0]);
        var userClaims = await _userManager.GetClaimsAsync(user);

        var filteredUserClaims = userClaims
        .Where(c => c.Type == FSHClaims.Permission
            && (c.Value.Contains(FSHResource.Appointment)
                || c.Value.Contains(FSHResource.Users)
                || c.Value.Contains(FSHResource.MedicalHistory)
                || c.Value.Contains(FSHResource.GeneralExamination)))
        .Select(c => c.Value)
        .ToList();

        var roleClaims = await _db.RoleClaims
            .Where(c => c.RoleId == role.Id
                && c.ClaimType == FSHClaims.Permission
                && (c.ClaimValue.Contains(FSHResource.Appointment)
                    || c.ClaimValue.Contains(FSHResource.Users)
                    || c.ClaimValue.Contains(FSHResource.MedicalHistory)
                    || c.ClaimValue.Contains(FSHResource.GeneralExamination)))
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);

        return filteredUserClaims.Union(roleClaims).Distinct().ToList();
    }

    public async Task<string> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request)
    {
        if (string.IsNullOrEmpty(request.Id))
        {
            // Create a new role.
            var role = new ApplicationRole(request.Name, request.Description);
            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                throw new InternalServerException(_t["Register role failed"], result.GetErrors(_t));
            }

            await _events.PublishAsync(new ApplicationRoleCreatedEvent(role.Id, role.Name!));

            return string.Format(_t["Role {0} Created."], request.Name);
        }
        else
        {
            // Update an existing role.
            var role = await _roleManager.FindByIdAsync(request.Id);

            _ = role ?? throw new NotFoundException(_t["Role Not Found"]);

            if (FSHRoles.IsDefault(role.Name!))
            {
                throw new ConflictException(string.Format(_t["Not allowed to modify {0} Role."], role.Name));
            }

            role.Name = request.Name;
            role.NormalizedName = request.Name.ToUpperInvariant();
            role.Description = request.Description;
            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                throw new InternalServerException(_t["Update role failed"], result.GetErrors(_t));
            }

            await _events.PublishAsync(new ApplicationRoleUpdatedEvent(role.Id, role.Name));

            return string.Format(_t["Role {0} Updated."], role.Name);
        }
    }

    public async Task AssignPermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserID) ?? throw new NotFoundException(_t["User Not Found"]);
        var roles = await _userManager.GetRolesAsync(user);

        if (roles[0] == FSHRoles.Admin || roles[0] == FSHRoles.Patient || roles[0] == FSHRoles.Guest)
        {
            throw new ConflictException(_t["Not allowed to modify Permissions for this Role."]);
        }

        if (_currentTenant.Id != MultitenancyConstants.Root.Id)
        {
            // Remove Root Permissions if the Role is not created for Root Tenant.
            throw new BadRequestException("The Role is not created for Root Tenant.");
        }
        var role = await _roleManager.FindByNameAsync(roles[0]);
        var currentClaims = await _roleManager.GetClaimsAsync(role);

        // Add all permissions that were not previously selected
        if (!currentClaims.Any(p => p.Value == FSHPermission.NameFor(request.Action, request.Resource)))
        {
            var list = new List<IdentityUserClaim<string>>();
            list.Add(new IdentityUserClaim<string>
            {
                UserId = request.UserID,
                ClaimType = FSHClaims.Permission,
                ClaimValue = FSHPermission.NameFor(request.Action, request.Resource)
            });
            if (request.Action != FSHAction.View &&
                !currentClaims.Any(p => p.Value == FSHPermission.NameFor(FSHAction.View, request.Resource)))
            {
                list.Add(new IdentityUserClaim<string>
                {
                    UserId = request.UserID,
                    ClaimType = FSHClaims.Permission,
                    ClaimValue = FSHPermission.NameFor(FSHAction.View, request.Resource)
                });
            }
            if (request.Resource.Equals(FSHResource.GeneralExamination) && request.Action.Equals(FSHAction.Create))
            {
                var additionalResources = new[] { FSHResource.Indication, FSHResource.Diagnosis, FSHResource.TreatmentPlanProcedures };
                foreach (var resource in additionalResources)
                {
                    list.Add(new IdentityUserClaim<string>
                    {
                        UserId = request.UserID,
                        ClaimType = FSHClaims.Permission,
                        ClaimValue = FSHPermission.NameFor(request.Action, resource)
                    });
                }
            }
            _db.UserClaims.AddRange(list);
            await _db.SaveChangesAsync(cancellationToken);
            await _events.PublishAsync(new ApplicationRoleUpdatedEvent(role.Id, role.Name!, true));
        }
        else
        {
            throw new BadRequestException("The Role had this permission");
        }
    }

    public async Task<string> DeleteAsync(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);

        _ = role ?? throw new NotFoundException(_t["Role Not Found"]);

        if (FSHRoles.IsDefault(role.Name!))
        {
            throw new ConflictException(string.Format(_t["Not allowed to delete {0} Role."], role.Name));
        }

        if ((await _userManager.GetUsersInRoleAsync(role.Name!)).Count > 0)
        {
            throw new ConflictException(string.Format(_t["Not allowed to delete {0} Role as it is being used."], role.Name));
        }

        await _roleManager.DeleteAsync(role);

        await _events.PublishAsync(new ApplicationRoleDeletedEvent(role.Id, role.Name!));

        return string.Format(_t["Role {0} Deleted."], role.Name);
    }

    public async Task<string> DeletePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserID) ?? throw new NotFoundException(_t["User Not Found"]);
        var roles = await _userManager.GetRolesAsync(user);

        if (roles[0] == FSHRoles.Admin || roles[0] == FSHRoles.Patient || roles[0] == FSHRoles.Guest)
        {
            throw new ConflictException(_t[$"Not allowed to modify Permissions for {roles[0]}."]);
        }

        if (_currentTenant.Id != MultitenancyConstants.Root.Id)
        {
            throw new BadRequestException(_t["The Role is not removable for Root Tenant."]);
        }
        var role = await _roleManager.FindByNameAsync(roles[0]);
        var currentClaims = await _roleManager.GetClaimsAsync(role);
        var roleclaimsToRemove = new List<Claim>();
        var userclaimsToRemove = new List<Claim>();
        var claimToRemove = currentClaims.FirstOrDefault(c =>
            c.Type == FSHClaims.Permission &&
            c.Value == FSHPermission.NameFor(request.Action, request.Resource));

        var userClaims = _userManager.GetClaimsAsync(user).Result;

        var userClaimToRemove = userClaims.FirstOrDefault(c =>
            c.Type == FSHClaims.Permission &&
            c.Value == FSHPermission.NameFor(request.Action, request.Resource));

        if (claimToRemove == null && userClaimToRemove == null)
        {
            throw new BadRequestException(_t["The Role does not have this permission"]);
        }

        if(claimToRemove != null)
            roleclaimsToRemove.Add(claimToRemove);
        if(userClaimToRemove != null)
            userclaimsToRemove.Add(userClaimToRemove);

        if (request.Resource == FSHResource.GeneralExamination && request.Action == FSHAction.Create)
        {
            var relatedResources = new[] { FSHResource.Indication, FSHResource.Diagnosis, FSHResource.TreatmentPlanProcedures };
            foreach (var relatedResource in relatedResources)
            {
                var relatedClaim = currentClaims.FirstOrDefault(c =>
                    c.Type == FSHClaims.Permission &&
                    c.Value == FSHPermission.NameFor(request.Action, relatedResource));
                if(relatedClaim == null)
                {
                    relatedClaim = userClaims.FirstOrDefault(c =>
                    c.Type == FSHClaims.Permission &&
                    c.Value == FSHPermission.NameFor(request.Action, relatedResource));
                    if( relatedClaim != null)
                    {
                        userclaimsToRemove.Add(relatedClaim);
                    }
                }
                else
                {
                    roleclaimsToRemove.Add(relatedClaim);
                }
            }
        }
        if (roleclaimsToRemove != null)
        {
            foreach (var claim in roleclaimsToRemove)
            {
                var removeResult = await _roleManager.RemoveClaimAsync(role, claim);
                if (!removeResult.Succeeded)
                {
                    throw new InternalServerException(_t["Delete permissions failed."], removeResult.GetErrors(_t));
                }
            }
        }
        if(userclaimsToRemove != null)
        {
            foreach (var claim in userclaimsToRemove)
            {
                var removeResult = await _userManager.RemoveClaimAsync(user, claim);
                if (!removeResult.Succeeded)
                {
                    throw new InternalServerException(_t["Delete permissions failed."], removeResult.GetErrors(_t));
                }
            }
        }
        return _t["Remove Permission Successfully"];
    }

    public async Task<List<RoleDto>> GetListWithPermissionAsync(CancellationToken cancellationToken)
    {
        var list = await _roleManager.Roles.Where(p => p.Name != FSHRoles.Admin && p.Name != FSHRoles.Guest && p.Name != FSHRoles.Patient).ToListAsync(cancellationToken);
        var roles = new List<RoleDto>();
        roles = list.Adapt<List<RoleDto>>();
        foreach (var role in roles)
        {
             role.Permissions = await _db.RoleClaims
            .Where(c => c.RoleId == role.Id && c.ClaimType == FSHClaims.Permission)
            .Select(c => c.ClaimValue!)
            .ToListAsync(cancellationToken);
        }
        return roles;
    }
}