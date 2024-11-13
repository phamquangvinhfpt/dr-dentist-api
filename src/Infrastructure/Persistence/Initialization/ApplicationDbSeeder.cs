using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Threading;

namespace FSH.WebApi.Infrastructure.Persistence.Initialization;

internal class ApplicationDbSeeder
{
    private readonly FSHTenantInfo _currentTenant;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CustomSeederRunner _seederRunner;
    private readonly ILogger<ApplicationDbSeeder> _logger;
    private readonly ApplicationDbContext _db;
    private readonly ISerializerService _serializerService;

    public ApplicationDbSeeder(ISerializerService serializerService, ApplicationDbContext db, FSHTenantInfo currentTenant, RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager, CustomSeederRunner seederRunner, ILogger<ApplicationDbSeeder> logger)
    {
        _currentTenant = currentTenant;
        _roleManager = roleManager;
        _userManager = userManager;
        _seederRunner = seederRunner;
        _logger = logger;
        _db = db;
        _serializerService = serializerService;
    }

    public async Task SeedDatabaseAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(dbContext);
        await SeedAdminUserAsync();
        await SeedAdmin2UserAsync();
        await SeedStaffUserAsync();
        await SeedServiceAsync();
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
                Id = "f56b04ea-d95d-4fab-be50-2fd2ca1561ff",
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
    private async Task SeedServiceAsync()
    {
        if (_db.Services.Count() < 1)
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dataPath = Path.Combine(path!, "Services", "ProcedureData.json");
            _logger.LogInformation("Started to Seed Service.");
            string proceData = await File.ReadAllTextAsync(dataPath);
            var procedures = _serializerService.Deserialize<List<Procedure>>(proceData);
            await _db.Procedures.AddRangeAsync(procedures);
            await _db.SaveChangesAsync();

            dataPath = Path.Combine(path!, "Services", "ServiceData.json");
            string serviceData = await File.ReadAllTextAsync(dataPath);
            var services = _serializerService.Deserialize<List<Service>>(serviceData);
            var totalPrice = GetToltalPrice(procedures);
            foreach (var service in services)
            {
                service.TotalPrice = totalPrice;
            }
            await _db.Services.AddRangeAsync(services);
            await _db.SaveChangesAsync();

            foreach (var service in services)
            {
                for (int i = 0; i < procedures.Count(); i++)
                {
                    _db.ServiceProcedures.Add(new ServiceProcedures
                    {
                        ServiceId = service.Id,
                        ProcedureId = procedures[i].Id,
                        StepOrder = i + 1
                    });
                }
            }
            await _db.SaveChangesAsync();
            var pros = await _db.Procedures.ToListAsync();
            foreach (var pro in pros)
            {
                pro.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            var sers = await _db.Services.ToListAsync();
            foreach (var i in sers)
            {
                i.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            var sps = await _db.ServiceProcedures.ToListAsync();
            foreach (var i in sps)
            {
                i.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Seeded Services.");
        }
    }
    private double GetToltalPrice(List<Procedure> procedures)
    {
        double result = 0;
        foreach (Procedure procedure in procedures)
        {
            result += procedure.Price;
        }
        return result;
    }
}