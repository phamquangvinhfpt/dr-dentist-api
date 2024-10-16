﻿using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSH.WebApi.Infrastructure.Persistence.Initialization;

internal class ApplicationDbSeeder
{
    private readonly FSHTenantInfo _currentTenant;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CustomSeederRunner _seederRunner;
    private readonly ILogger<ApplicationDbSeeder> _logger;

    public ApplicationDbSeeder(FSHTenantInfo currentTenant, RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager, CustomSeederRunner seederRunner, ILogger<ApplicationDbSeeder> logger)
    {
        _currentTenant = currentTenant;
        _roleManager = roleManager;
        _userManager = userManager;
        _seederRunner = seederRunner;
        _logger = logger;
    }

    public async Task SeedDatabaseAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(dbContext);
        await SeedAdminUserAsync();
        await SeedAdmin2UserAsync();
        await SeedBasicUserAsync();
        await SeedStaffUserAsync();
        await _seederRunner.RunSeedersAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(ApplicationDbContext dbContext)
    {
        foreach (string roleName in FSHRoles.DefaultRoles)
        {
            if (await _roleManager.Roles.SingleOrDefaultAsync(r => r.Name == roleName)
                is not ApplicationRole role)
            {
                // Create the role
                _logger.LogInformation("Seeding {role} Role for '{tenantId}' Tenant.", roleName, _currentTenant.Id);
                role = new ApplicationRole(roleName, $"{roleName} Role for {_currentTenant.Id} Tenant");
                await _roleManager.CreateAsync(role);
            }

            // Assign permissions
            if (roleName == FSHRoles.Dentist)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Dentist, role);
            }
            else if (roleName == FSHRoles.Admin)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Admin, role);

                if (_currentTenant.Id == MultitenancyConstants.Root.Id)
                {
                   await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Root, role);
                }
            }
            else if (roleName == FSHRoles.Staff)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Staff, role);
            }
            else if (roleName == FSHRoles.Patient)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Patient, role);
            }
            else if (roleName == FSHRoles.Guest)
            {
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Guest, role);
            }
        }
    }

    private async Task AssignPermissionsToRoleAsync(ApplicationDbContext dbContext, IReadOnlyList<FSHPermission> permissions, ApplicationRole role)
    {
        var currentClaims = await _roleManager.GetClaimsAsync(role);
        foreach (var permission in permissions)
        {
            if (!currentClaims.Any(c => c.Type == FSHClaims.Permission && c.Value == permission.Name))
            {
                _logger.LogInformation("Seeding {role} Permission '{permission}' for '{tenantId}' Tenant.", role.Name, permission.Name, _currentTenant.Id);
                dbContext.RoleClaims.Add(new ApplicationRoleClaim
                {
                    RoleId = role.Id,
                    ClaimType = FSHClaims.Permission,
                    ClaimValue = permission.Name,
                    CreatedBy = "ApplicationDbSeeder"
                });
                await dbContext.SaveChangesAsync();
            }
        }
    }

    private async Task SeedAdminUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace(_currentTenant.AdminEmail))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == _currentTenant.AdminEmail)
            is not ApplicationUser adminUser)
        {
            string adminUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Admin}".ToLowerInvariant();
            adminUser = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Admin,
                Email = _currentTenant.AdminEmail,
                UserName = adminUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = _currentTenant.AdminEmail?.ToUpperInvariant(),
                NormalizedUserName = adminUserName.ToUpperInvariant(),
                IsActive = true
            };

            _logger.LogInformation("Seeding Default Admin User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            adminUser.PasswordHash = password.HashPassword(adminUser, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(adminUser);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(adminUser, FSHRoles.Admin))
        {
            _logger.LogInformation("Assigning Admin Role to Admin User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(adminUser, FSHRoles.Admin);
        }
    }

    private async Task SeedAdmin2UserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace("admin2@root.com"))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "admin2@root.com")
                       is not ApplicationUser admin2User)
        {
            admin2User = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Admin,
                Email = "admin2@root.com",
                UserName = "admin2.root",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "admin2@root.com"?.ToUpperInvariant(),
                NormalizedUserName = "admin2.root".ToUpperInvariant(),
                IsActive = true
            };

            _logger.LogInformation("Seeding Default Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            admin2User.PasswordHash = password.HashPassword(admin2User, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(admin2User);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(admin2User, FSHRoles.Admin))
        {
            _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(admin2User, FSHRoles.Admin);
        }
    }

    private async Task SeedBasicUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace("basic@root.com"))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "basic@root.com")
                       is not ApplicationUser basicUser)
        {
            string basicUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Patient}".ToLowerInvariant();
            basicUser = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Patient,
                Email = "basic@root.com",
                UserName = basicUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "basic@root.com"?.ToUpperInvariant(),
                NormalizedUserName = basicUserName.ToUpperInvariant(),
                IsActive = true
            };

            _logger.LogInformation("Seeding Default Basic User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            basicUser.PasswordHash = password.HashPassword(basicUser, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(basicUser);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(basicUser, FSHRoles.Patient))
        {
            _logger.LogInformation("Assigning Basic Role to Basic User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(basicUser, FSHRoles.Patient);
        }
    }

    private async Task SeedStaffUserAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentTenant.Id) || string.IsNullOrWhiteSpace("staff@root.com"))
        {
            return;
        }

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "staff@root.com")
                          is not ApplicationUser staffUser)
        {
            string staffUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Staff}".ToLowerInvariant();
            staffUser = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Staff,
                Email = "staff@root.com",
                UserName = staffUserName,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "staff@root.com"?.ToUpperInvariant(),
                NormalizedUserName = staffUserName.ToUpperInvariant(),
                IsActive = true
            };

            _logger.LogInformation("Seeding Default Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            staffUser.PasswordHash = password.HashPassword(staffUser, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(staffUser);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(staffUser, FSHRoles.Staff))
        {
            _logger.LogInformation("Assigning Staff Role to Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(staffUser, FSHRoles.Staff);
        }
    }
}