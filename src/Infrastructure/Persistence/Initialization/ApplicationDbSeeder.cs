using DocumentFormat.OpenXml.Spreadsheet;
using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Appointments;
using FSH.WebApi.Domain.CustomerServices;
using FSH.WebApi.Domain.Identity;
using FSH.WebApi.Domain.Payments;
using FSH.WebApi.Domain.Service;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Shared.Authorization;
using FSH.WebApi.Shared.Multitenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
    private readonly IServiceScopeFactory _scopeFactory;

    public ApplicationDbSeeder(ISerializerService serializerService, ApplicationDbContext db, FSHTenantInfo currentTenant, RoleManager<ApplicationRole> roleManager, UserManager<ApplicationUser> userManager, CustomSeederRunner seederRunner, ILogger<ApplicationDbSeeder> logger, IServiceScopeFactory scopeFactory)
    {
        _currentTenant = currentTenant;
        _roleManager = roleManager;
        _userManager = userManager;
        _seederRunner = seederRunner;
        _logger = logger;
        _db = db;
        _serializerService = serializerService;
        _scopeFactory = scopeFactory;
    }

    public async Task SeedDatabaseAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(dbContext);
        await SeedAdminUserAsync();
        await SeedAdmin2UserAsync();
        await SeedStaffUserAsync();
        await SeedTypeServiceAsync();
        await SeedUserAsync();
        await SeedRoomAsync();
        await SeedWorkingCalendar();
        await SeedServiceAsync();
        await SeedAppointmentAsync();
        await SeedAppointmentInforAsync();
        await _seederRunner.RunSeedersAsync(cancellationToken);
    }

    private async Task SeedRoomAsync()
    {

        if (!_db.Rooms.Any())
        {
            _logger.LogInformation("Seeding room");
            int number = 5;
            for (int i = 1; i <= 5; i++)
            {
                _db.Rooms.Add(new Domain.Examination.Room
                {
                    RoomName = $"Room {i}",
                    Status = true
                });
            }
            await _db.SaveChangesAsync();
        }
    }
    private async Task SeedTypeServiceAsync()
    {
        if (!_db.TypeServices.Any())
        {
            _logger.LogInformation("Seeding Type Service.");
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dataPath = Path.Combine(path!, "Services", "TypeServiceData.json");
            _logger.LogInformation("Started to Seed Type Service.");

            string serviceData = await File.ReadAllTextAsync(dataPath);
            var services = _serializerService.Deserialize<List<TypeService>>(serviceData);
            await _db.TypeServices.AddRangeAsync(services);
            await _db.SaveChangesAsync();
        }
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
                await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.Root, role);

                if (_currentTenant.Id == MultitenancyConstants.Root.Id)
                {
                    await AssignPermissionsToRoleAsync(dbContext, FSHPermissions.All, role);
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

        if (await _userManager.Users.FirstOrDefaultAsync(u => u.Email == "staff@root.com")
                          is not ApplicationUser staffUser2)
        {
            string staffUserName = $"{_currentTenant.Id.Trim()}.{FSHRoles.Staff}".ToLowerInvariant();

            staffUser2 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = $"{FSHRoles.Staff} 2",
                Email = "staff2@root.com",
                UserName = "Staff 2",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "staff2@root.com"?.ToUpperInvariant(),
                NormalizedUserName = "Staff 2".ToUpperInvariant(),
                IsActive = true
            };
            _logger.LogInformation("Seeding Default Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            staffUser2.PasswordHash = password.HashPassword(staffUser2, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(staffUser2);
        }

        // Assign role to user
        if (!await _userManager.IsInRoleAsync(staffUser, FSHRoles.Staff))
        {
            _logger.LogInformation("Assigning Staff Role to Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(staffUser, FSHRoles.Staff);
        }
        if (!await _userManager.IsInRoleAsync(staffUser2, FSHRoles.Staff))
        {
            _logger.LogInformation("Assigning Staff Role to Staff User for '{tenantId}' Tenant.", _currentTenant.Id);
            await _userManager.AddToRoleAsync(staffUser2, FSHRoles.Staff);
        }
    }
    private async Task SeedServiceAsync()
    {
        _logger.LogInformation("Seeding Service.");
        if (_db.Services.Count() < 1)
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string dataPath = Path.Combine(path!, "Services", "ServiceData.json");
            _logger.LogInformation("Started to Seed Service.");

            string serviceData = await File.ReadAllTextAsync(dataPath);
            var services = _serializerService.Deserialize<List<Service>>(serviceData);
            Random next = new Random();
            var type = await _db.TypeServices.ToListAsync();
            foreach (var service in services)
            {
                var t = type[next.Next(type.Count())];
                service.TypeServiceID = t.Id;
            }

            await _db.Services.AddRangeAsync(services);
            await _db.SaveChangesAsync();

            var sers = await _db.Services.ToListAsync();
            foreach (var i in sers)
            {
                i.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
                i.CreatedOn = DateTime.Now;
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Seeded Services.");
            await SeedServiceProcedure1Async();
            await SeedServiceProcedure2Async();
            await SeedServiceProcedure3Async();
            await SeedServiceProcedure4Async();
            await SeedServiceProcedure5Async();
            await SeedServiceProcedure6Async();
            await SeedServiceProcedure7Async();
            await SeedServiceProcedure8Async();
            await SeedServiceProcedure9Async();
            await SeedServiceProcedure10Async();
            await SeedProcedureInforAsync();
        }
    }
    private async Task SeedServiceProcedure1Async()
    {
        _logger.LogInformation("Seeding Service Procedure 1.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure1.json");
        _logger.LogInformation("Started to Seed Service Procedure 1.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Khám Răng Tổng Quát Cơ Bản");
        int step = 1;
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 1.");
    }

    private async Task SeedServiceProcedure2Async()
    {
        _logger.LogInformation("Seeding Service Procedure 2.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure2.json");
        _logger.LogInformation("Started to Seed Service Procedure 2.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Tầm Soát Ung Thư Khoang Miệng");
        int step = 1;
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 2.");
    }
    private async Task SeedServiceProcedure3Async()
    {
        _logger.LogInformation("Seeding Service Procedure 3.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure3.json");
        _logger.LogInformation("Started to Seed Service Procedure 3.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Điều Trị Nha Chu");
        int step = 1;
        var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Name == "Khám và Tư Vấn Răng Miệng");
        if (pro != null)
        {
            ser.TotalPrice += pro.Price;
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = ser.Id,
                ProcedureId = pro.Id,
                StepOrder = step++,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                CreatedOn = DateTime.Now,
            });
        }
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 3.");
    }
    private async Task SeedServiceProcedure4Async()
    {
        _logger.LogInformation("Seeding Service Procedure 4.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure4.json");
        _logger.LogInformation("Started to Seed Service Procedure 4.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Chỉnh Nha Cơ Bản");
        int step = 1;
        var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Name == "Khám và Tư Vấn Răng Miệng");
        if (pro != null)
        {
            ser.TotalPrice += pro.Price;
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = ser.Id,
                ProcedureId = pro.Id,
                StepOrder = step++,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                CreatedOn = DateTime.Now,
            });
        }
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 4.");
    }
    private async Task SeedServiceProcedure5Async()
    {
        _logger.LogInformation("Seeding Service Procedure 5.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure5.json");
        _logger.LogInformation("Started to Seed Service Procedure 5.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Implant Nha Khoa");
        int step = 1;
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 5.");
    }
    private async Task SeedServiceProcedure6Async()
    {
        _logger.LogInformation("Seeding Service Procedure 6.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure6.json");
        _logger.LogInformation("Started to Seed Service Procedure 6.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Tẩy Trắng Răng");
        int step = 1;
        var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Name == "Vệ Sinh Răng Miệng Chuyên Sâu");
        if (pro != null)
        {
            ser.TotalPrice += pro.Price;
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = ser.Id,
                ProcedureId = pro.Id,
                StepOrder = 3,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                CreatedOn = DateTime.Now,
            });
        }
        foreach (var i in services)
        {
            if (step == 3)
            {
                step += 1;
            }
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 6.");
    }
    private async Task SeedServiceProcedure7Async()
    {
        _logger.LogInformation("Seeding Service Procedure 7.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure7.json");
        _logger.LogInformation("Started to Seed Service Procedure 7.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Phục Hình Răng Sứ");
        int step = 1;
        var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Name == "Khám và Tư Vấn Răng Miệng");
        if (pro != null)
        {
            ser.TotalPrice += pro.Price;
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = ser.Id,
                ProcedureId = pro.Id,
                StepOrder = step++,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                CreatedOn = DateTime.Now,
            });
        }
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 7.");
    }
    private async Task SeedServiceProcedure8Async()
    {
        _logger.LogInformation("Seeding Service Procedure 8.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure8.json");
        _logger.LogInformation("Started to Seed Service Procedure 8.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Nha Khoa Trẻ Em");
        int step = 1;
        var pro = await _db.Procedures.FirstOrDefaultAsync(p => p.Name == "Vệ Sinh Răng Miệng Chuyên Sâu");
        if (pro != null)
        {
            ser.TotalPrice += pro.Price;
            _db.ServiceProcedures.Add(new ServiceProcedures
            {
                ServiceId = ser.Id,
                ProcedureId = pro.Id,
                StepOrder = 3,
                CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                CreatedOn = DateTime.Now,
            });
        }
        foreach (var i in services)
        {
            if (step == 3)
            {
                step += 1;
            }
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 8.");
    }

    private async Task SeedServiceProcedure9Async()
    {
        _logger.LogInformation("Seeding Service Procedure 9.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "Procedure9.json");
        _logger.LogInformation("Started to Seed Service Procedure 9.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Điều Trị Tủy Răng");
        int step = 1;
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 9.");
    }

    private async Task SeedServiceProcedure10Async()
    {
        _logger.LogInformation("Seeding Service Procedure 10.");
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Services", "procedure10.json");
        _logger.LogInformation("Started to Seed Service Procedure 10.");

        string serviceData = await File.ReadAllTextAsync(dataPath);
        var services = _serializerService.Deserialize<List<Procedure>>(serviceData);

        var ser = await _db.Services.FirstOrDefaultAsync(p => p.ServiceName == "Gói Niềng Răng Thẩm Mỹ");
        int step = 1;
        foreach (var i in services)
        {
            var procedure = _db.Procedures.Add(i).Entity;
            if (ser != null)
            {
                ser.TotalPrice += procedure.Price;
                _db.ServiceProcedures.Add(new ServiceProcedures
                {
                    ServiceId = ser.Id,
                    ProcedureId = procedure.Id,
                    StepOrder = step++,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff"),
                    CreatedOn = DateTime.Now,
                });
            }
        }
        ser.IsActive = true;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Services Procedure 10.");
    }

    private async Task SeedProcedureInforAsync()
    {
        _logger.LogInformation("Seeding Procedure Information.");
        var procedures = _db.Procedures.ToList();

        foreach (var procedure in procedures)
        {
            procedure.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            procedure.CreatedOn = DateTime.Now;
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded Procedure Information.");
    }
    private async Task SeedAppointmentAsync()
    {
        _logger.LogInformation("Seeding Appointments...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!await dbContext.Appointments.AnyAsync())
            {
                var serviceProcedures = await dbContext.ServiceProcedures
                    .GroupBy(p => p.ServiceId)
                    .Select(p => new
                    {
                        ServiceID = p.Key,
                        ProcedureIDs = p.Select(p => p.ProcedureId).ToList(),
                    })
                    .ToListAsync();

                var patients = await dbContext.PatientProfiles.Select(p => new { PatientID = p.Id }).ToListAsync();
                var currentDate = DateOnly.FromDateTime(DateTime.Now);

                var appointmentsToAdd = new ConcurrentBag<Appointment>();

                await Parallel.ForEachAsync(Enumerable.Range(-270, 271), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (dayOffset, ct) =>
                {
                    using var innerScope = _scopeFactory.CreateScope();
                    var innerDbContext = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    var random = new Random(Guid.NewGuid().GetHashCode());
                    var appointmentDate = currentDate.AddDays(dayOffset);

                    if (appointmentDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        return;
                    }

                    var availableDoctors = await innerDbContext.WorkingCalendars
                        .Where(w => w.Date == appointmentDate && w.Status == WorkingStatus.Accept)
                        .Select(w => new
                        {
                            DoctorId = w.DoctorID,
                            CalendarId = w.Id
                        })
                        .ToListAsync(ct);

                    if (!availableDoctors.Any())
                    {
                        return;
                    }

                    var appointmentsPerDay = random.Next(4, 7);

                    for (int i = 0; i < appointmentsPerDay; i++)
                    {
                        var selectedDoctor = availableDoctors[random.Next(availableDoctors.Count())];

                        var doctorTimeSlots = await innerDbContext.TimeWorkings
                            .Where(t => t.CalendarID == selectedDoctor.CalendarId && t.IsActive)
                            .ToListAsync(ct);

                        if (!doctorTimeSlots.Any())
                        {
                            continue;
                        }

                        TimeSpan startTime = TimeSpan.Zero;
                        TimeSpan duration = TimeSpan.FromMinutes(30);
                        bool slotFound = false;
                        int attempts = 0;

                        do
                        {
                            var selectedTimeSlot = doctorTimeSlots[random.Next(doctorTimeSlots.Count())];
                            var maxStartTime = selectedTimeSlot.EndTime - duration;

                            int availableMinutes = (int)(maxStartTime - selectedTimeSlot.StartTime).TotalMinutes;
                            int randomOffsetMinutes = random.Next(0, availableMinutes);
                            startTime = selectedTimeSlot.StartTime.Add(TimeSpan.FromMinutes(randomOffsetMinutes));

                            bool isTimeSlotBooked = await innerDbContext.AppointmentCalendars
                                .AnyAsync(c =>
                                    c.DoctorId == selectedDoctor.DoctorId &&
                                    c.Date == appointmentDate &&
                                    (
                                        c.StartTime <= startTime && startTime < c.EndTime ||
                                        c.StartTime < startTime.Add(duration) && startTime.Add(duration) <= c.EndTime ||
                                        startTime <= c.StartTime && c.EndTime <= startTime.Add(duration)
                                    ) &&
                                    (
                                        c.Status == CalendarStatus.Booked ||
                                        c.Status == CalendarStatus.Waiting
                                    ), ct);

                            if (!isTimeSlotBooked)
                            {
                                slotFound = true;
                            }

                            attempts++;
                            if (attempts > 10)
                            {
                                break;
                            }

                        } while (!slotFound);

                        if (!slotFound)
                        {
                            continue;
                        }

                        var patientId = patients[random.Next(patients.Count())];
                        var service = serviceProcedures[random.Next(serviceProcedures.Count())];

                        AppointmentStatus status;
                        bool canProvideFeedback = false;

                        if (dayOffset < 0)
                        {
                            status = AppointmentStatus.Come;
                            canProvideFeedback = true;
                        }
                        else if (dayOffset == 0)
                        {
                            var currentTime = DateTime.Now.TimeOfDay;
                            if (startTime < currentTime)
                            {
                                status = AppointmentStatus.Come;
                                canProvideFeedback = true;
                            }
                            else
                            {
                                status = AppointmentStatus.Confirmed;
                            }
                        }
                        else
                        {
                            status = AppointmentStatus.Confirmed;
                        }

                        var appointment = new Appointment
                        {
                            PatientId = patientId.PatientID,
                            DentistId = selectedDoctor.DoctorId,
                            ServiceId = service.ServiceID.Value,
                            AppointmentDate = appointmentDate,
                            StartTime = startTime,
                            Duration = duration,
                            Status = status,
                            SpamCount = 0,
                            canFeedback = canProvideFeedback,
                            CreatedOn = DateTime.Now.AddDays(dayOffset - random.Next(0, 3)),
                            CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                        };

                        appointmentsToAdd.Add(appointment);
                    }
                });

                // Batch insert
                var batchSize = 1000;
                var appointmentsList = appointmentsToAdd.ToList();
                for (int i = 0; i < appointmentsList.Count; i += batchSize)
                {
                    var batch = appointmentsList.Skip(i).Take(batchSize);
                    await dbContext.Appointments.AddRangeAsync(batch);
                    await dbContext.SaveChangesAsync();

                    _logger.LogInformation($"Batch {i / batchSize + 1} inserted. Total {batch.Count()} appointments.");
                }

                _logger.LogInformation($"Appointments seeding completed successfully. Total: {appointmentsList.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding appointments");
            throw;
        }
    }

    private async Task SeedNonDoctorAppointmentAsync()
    {
        _logger.LogInformation("Seeded Appointment.");
        try
        {
            var serviceProcedures = _db.ServiceProcedures
                .GroupBy(p => p.ServiceId)
                .Select(p => new
                {
                    ServiceID = p.Key,
                    ProcedureIDs = p.Select(p => p.ProcedureId).ToList(),
                })
                .ToList();

            var patients = _db.PatientProfiles.Select(p => new { PatientID = p.Id }).ToList();

            var appointments = new List<Appointment>();
            var random = new Random();

            var currentDate = DateOnly.FromDateTime(DateTime.Now);

            for (int dayOffset = 5; dayOffset <= 20; dayOffset++)
            {
                var appointmentDate = currentDate.AddDays(dayOffset);

                if (appointmentDate.DayOfWeek == DayOfWeek.Saturday ||
                    appointmentDate.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                var appointmentsPerDay = random.Next(5, 11);
                var startHour = random.Next(8, 17);
                var startTime = new TimeSpan(startHour, 0, 0);

                var duration = TimeSpan.FromMinutes(30);

                var patientId = patients[random.Next(patients.Count)];
                var service = serviceProcedures[random.Next(serviceProcedures.Count)];

                var appointment = _db.Appointments.Add(new Appointment
                {
                    PatientId = patientId.PatientID,
                    ServiceId = service.ServiceID.Value,
                    AppointmentDate = appointmentDate,
                    StartTime = startTime,
                    Duration = duration,
                    Status = AppointmentStatus.Confirmed,
                    SpamCount = 0,
                    canFeedback = false,
                    CreatedOn = DateTime.Now.AddDays(dayOffset - random.Next(0, 3)),
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                }).Entity;

                var s = _db.Services.FirstOrDefault(p => p.Id == appointment.ServiceId);
                var payment = _db.Payments.Add(new Domain.Payments.Payment
                {
                    PatientProfileId = appointment.PatientId,
                    AppointmentId = appointment.Id,
                    ServiceId = appointment.ServiceId,
                    DepositAmount = (s.TotalPrice * 0.3),
                    DepositDate = DateOnly.FromDateTime(DateTime.Now),
                    Amount = s.TotalPrice,
                    RemainingAmount = s.TotalPrice - (s.TotalPrice * 0.3),
                    Method = Domain.Payments.PaymentMethod.None,
                    Status = Domain.Payments.PaymentStatus.Incomplete,
                }).Entity;
                var payDetail = new List<Domain.Payments.PaymentDetail>();
                var sps = _db.ServiceProcedures.Where(p => p.ServiceId == s.Id)
                    .OrderBy(p => p.StepOrder)
                    .Select(s => new
                    {
                        SP = s,
                        Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                    })
                    .ToList();
                var val = _db.AppointmentCalendars.Add(new AppointmentCalendar
                {
                    PatientId = patientId.PatientID,
                    AppointmentId = appointment.Id,
                    Date = appointment.AppointmentDate,
                    StartTime = appointment.StartTime,
                    EndTime = appointment.StartTime.Add(appointment.Duration),
                    Status = Domain.Identity.CalendarStatus.Booked,
                    Type = AppointmentType.Appointment,
                });
                foreach (var item in sps)
                {
                    payDetail.Add(new Domain.Payments.PaymentDetail
                    {
                        PaymentID = payment.Id,
                        ProcedureID = item.Procedure.Id,
                        PaymentAmount = item.Procedure.Price,
                        PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                    });
                }
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Successfully seeded {appointments.Count} appointments.");
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }

    private async Task SeedAppointmentInforAsync()
    {
        try
        {
            _logger.LogInformation($"Seeding Payment");
            if (!_db.Payments.Any())
            {
                var random = new Random();
                var currentDate = DateOnly.FromDateTime(DateTime.Now);
                var appointments = _db.Appointments.ToList();

                foreach (var appointment in appointments)
                {
                    if (appointment.Status == AppointmentStatus.Come)
                    {
                        var s = _db.Services.FirstOrDefault(p => p.Id == appointment.ServiceId);
                        var payment = _db.Payments.Add(new Domain.Payments.Payment
                        {
                            PatientProfileId = appointment.PatientId,
                            AppointmentId = appointment.Id,
                            ServiceId = appointment.ServiceId,
                            DepositAmount = s.TotalPrice * 0.3,
                            DepositDate = DateOnly.FromDateTime(appointment.CreatedOn),
                            Amount = s.TotalPrice,
                            RemainingAmount = 0,
                            Method = Domain.Payments.PaymentMethod.BankTransfer,
                            Status = Domain.Payments.PaymentStatus.Completed,
                            FinalPaymentDate = appointment.AppointmentDate,
                        }).Entity;
                        var payDetail = new List<Domain.Payments.PaymentDetail>();
                        var sps = _db.ServiceProcedures.Where(p => p.ServiceId == s.Id)
                            .OrderBy(p => p.StepOrder)
                            .Select(s => new
                            {
                                SP = s,
                                Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                            })
                            .ToList();
                        var next = 4;
                        var d = appointment.AppointmentDate;
                        foreach (var item in sps)
                        {
                            payDetail.Add(new Domain.Payments.PaymentDetail
                            {
                                PaymentID = payment.Id,
                                ProcedureID = item.Procedure.Id,
                                PaymentAmount = item.Procedure.Price,
                                PaymentStatus = Domain.Payments.PaymentStatus.Completed,
                            });
                            var date = d.AddDays(next++);
                            // seed treatment plan
                            bool c = date > currentDate;
                            var t = _db.TreatmentPlanProcedures.Add(new Domain.Treatment.TreatmentPlanProcedures
                            {
                                ServiceProcedureId = item.SP.Id,
                                AppointmentID = appointment.Id,
                                DoctorID = appointment.DentistId,
                                Status = c ? Domain.Treatment.TreatmentPlanStatus.Active : Domain.Treatment.TreatmentPlanStatus.Completed,
                                Cost = item.Procedure.Price,
                                StartTime = appointment.StartTime,
                                DiscountAmount = 0,
                                FinalCost = item.Procedure.Price,
                                StartDate = date,
                            }).Entity;
                            var w = _db.AppointmentCalendars.Add(new Domain.Identity.AppointmentCalendar
                            {
                                DoctorId = appointment.DentistId,
                                PatientId = appointment.PatientId,
                                AppointmentId = appointment.Id,
                                PlanID = t.Id,
                                Date = t.StartDate,
                                StartTime = t.StartTime,
                                EndTime = t.StartTime.Value.Add(TimeSpan.FromMinutes(30)),
                                Status = c ? Domain.Identity.CalendarStatus.Booked : CalendarStatus.Success,
                                Type = item.SP.StepOrder == 1 ? AppointmentType.Appointment : AppointmentType.FollowUp,
                            });

                            if (!c)
                            {
                                var pre = _db.Prescriptions.Add(new Domain.Treatment.Prescription
                                {
                                    TreatmentID = t.Id,
                                    DoctorID = appointment.DentistId,
                                    PatientID = appointment.PatientId,
                                    Notes = "Use every day"
                                }).Entity;

                                var preItem = _db.PrescriptionItems.Add(new Domain.Treatment.PrescriptionItem
                                {
                                    PrescriptionId = pre.Id,
                                    MedicineName = "Chống viêm",
                                    Dosage = "1 viên 1 lần",
                                    Frequency = "1 lần 1 ngày",
                                });
                                appointment.Status = AppointmentStatus.Done;
                            }
                            else
                            {
                                appointment.Status = AppointmentStatus.Come;
                            }
                        }

                        // Seed medical record

                        var medical = _db.MedicalRecords.Add(new Domain.Examination.MedicalRecord
                        {
                            DoctorProfileId = appointment.DentistId,
                            PatientProfileId = appointment.PatientId,
                            Date = DateTime.Parse(appointment.AppointmentDate.ToString()),
                            AppointmentId = appointment.Id,
                        }).Entity;

                        _db.BasicExaminations.Add(new Domain.Examination.BasicExamination
                        {
                            RecordId = medical.Id,
                            ExaminationContent = "Good",
                            TreatmentPlanNote = "Ok"
                        });

                        _db.Diagnoses.Add(new Domain.Examination.Diagnosis
                        {
                            RecordId = medical.Id,
                            ToothNumber = 5,
                            TeethConditions = new[] { "Sâu Răng" }
                        });

                        _db.Indications.Add(new Domain.Examination.Indication
                        {
                            RecordId = medical.Id,
                            IndicationType = new[] { "Khác" },
                            Description = "Sâu Răng"
                        });

                        if (appointment.Status == AppointmentStatus.Done)
                        {
                            _db.Feedbacks.Add(new Feedback
                            {
                                PatientProfileId = appointment.PatientId,
                                DoctorProfileId = appointment.DentistId,
                                ServiceId = s.Id,
                                AppointmentId = appointment.Id,
                                Rating = random.Next(3, 5),
                                Message = $"Feedback for appointment on {appointment.AppointmentDate}",
                                CreatedBy = appointment.PatientId
                            });
                        }

                    }
                    else
                    {
                        var s = _db.Services.FirstOrDefault(p => p.Id == appointment.ServiceId);
                        var dAmount = 0.3;
                        var payment = _db.Payments.Add(new Domain.Payments.Payment
                        {
                            PatientProfileId = appointment.PatientId,
                            AppointmentId = appointment.Id,
                            ServiceId = appointment.ServiceId,
                            DepositAmount = (s.TotalPrice * dAmount),
                            DepositDate = DateOnly.FromDateTime(appointment.CreatedOn),
                            Amount = s.TotalPrice,
                            RemainingAmount = s.TotalPrice - (s.TotalPrice * dAmount),
                            Method = Domain.Payments.PaymentMethod.None,
                            Status = Domain.Payments.PaymentStatus.Incomplete,
                        }).Entity;
                        var payDetail = new List<Domain.Payments.PaymentDetail>();
                        var sps = _db.ServiceProcedures.Where(s => s.ServiceId == s.Id)
                            .OrderBy(p => p.StepOrder)
                            .Select(s => new
                            {
                                SP = s,
                                Procedure = _db.Procedures.FirstOrDefault(p => p.Id == s.ProcedureId),
                            })
                            .ToList();
                        var next = 1;
                        foreach (var item in sps)
                        {
                            payDetail.Add(new Domain.Payments.PaymentDetail
                            {
                                PaymentID = payment.Id,
                                ProcedureID = item.Procedure.Id,
                                PaymentAmount = item.Procedure.Price,
                                PaymentStatus = Domain.Payments.PaymentStatus.Incomplete,
                            });

                            // seed treatment plan
                            var t = new Domain.Treatment.TreatmentPlanProcedures
                            {
                                ServiceProcedureId = item.SP.Id,
                                AppointmentID = appointment.Id,
                                DoctorID = appointment.DentistId,
                                Status = item.SP.StepOrder == 1 ? Domain.Treatment.TreatmentPlanStatus.Active : Domain.Treatment.TreatmentPlanStatus.Pending,
                                Cost = item.Procedure.Price,
                                StartTime = appointment.StartTime,
                                DiscountAmount = 0,
                                FinalCost = item.Procedure.Price,
                            };
                            if (item.SP.StepOrder == 1)
                            {
                                t.StartDate = appointment.AppointmentDate.AddDays(+next++);
                            }
                            t = _db.TreatmentPlanProcedures.Add(t).Entity;
                        }
                        _db.AppointmentCalendars.Add(new Domain.Identity.AppointmentCalendar
                        {
                            PatientId = appointment.PatientId,
                            DoctorId = appointment.DentistId,
                            AppointmentId = appointment.Id,
                            Date = appointment.AppointmentDate,
                            StartTime = appointment.StartTime,
                            EndTime = appointment.StartTime.Add(TimeSpan.FromMinutes(30)),
                            Status = Domain.Identity.CalendarStatus.Booked,
                            Type = AppointmentType.Appointment,
                        });
                    }
                }
                _db.SaveChanges();
                await SeedNonDoctorAppointmentAsync();
                await SeedPatientDemoAsync();
            }
            _logger.LogInformation($"Seeded Payment");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
            throw;
        }
    }



    private async Task SeedUserAsync()
    {
        _logger.LogInformation("Seeding Users.");
        if (_db.Users.Count() <= 5)
        {
            string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string userDataPath = Path.Combine(path!, "Identity", "UserData.json");
            string patientDataPath = Path.Combine(path!, "Identity", "PatientProfileData.json");
            string doctorDataPath = Path.Combine(path!, "Identity", "DoctorProfileData.json");
            _logger.LogInformation("Started to Seed Users.");
            string userData = await File.ReadAllTextAsync(userDataPath);
            string patientData = await File.ReadAllTextAsync(patientDataPath);
            string doctorData = await File.ReadAllTextAsync(doctorDataPath);
            var users = _serializerService.Deserialize<List<ApplicationUser>>(userData);
            var patients = _serializerService.Deserialize<List<PatientProfile>>(patientData);
            var doctors = _serializerService.Deserialize<List<DoctorProfile>>(doctorData);
            List<ApplicationUser> doctor = new List<ApplicationUser>();
            List<ApplicationUser> patient = new List<ApplicationUser>();
            int flash = 0;
            int d_profile_index = 0;
            int p_profile_index = 0;
            Random next = new Random();
            var type = await _db.TypeServices.ToListAsync();
            foreach (var user in users)
            {
                var entry = _db.Users.Add(user).Entity;
                if (flash < 5)
                {
                    var t = type[next.Next(type.Count)];
                    doctors[d_profile_index].DoctorId = entry.Id;
                    doctors[d_profile_index].TypeServiceID = t.Id;
                    doctors[d_profile_index].WorkingType = WorkingType.FullTime;
                    doctors[d_profile_index].IsActive = true;
                    doctor.Add(entry);
                    flash++;
                    d_profile_index++;
                }
                else if (flash < 10)
                {
                    var t = type[next.Next(type.Count)];
                    doctors[d_profile_index].DoctorId = entry.Id;
                    doctors[d_profile_index].TypeServiceID = t.Id;
                    doctors[d_profile_index].WorkingType = WorkingType.PartTime;
                    doctors[d_profile_index].IsActive = true;
                    doctor.Add(entry);
                    flash++;
                    d_profile_index++;
                }
                else
                {
                    patients[p_profile_index].UserId = entry.Id;
                    patient.Add(entry);
                    p_profile_index++;
                }
            }
            await _db.DoctorProfiles.AddRangeAsync(doctors);
            await _db.PatientProfiles.AddRangeAsync(patients);
            await _db.SaveChangesAsync();
            foreach (var user in doctor)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Dentist))
                {
                    _logger.LogInformation("Assigning Dentist Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Dentist);
                }
                var profile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == user.Id);
                profile.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            foreach (var user in patient)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Patient))
                {
                    _logger.LogInformation("Assigning Patient Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Patient);
                }
            }
            foreach (var user in doctor)
            {
                var profile = await _db.DoctorProfiles.FirstOrDefaultAsync(p => p.DoctorId == user.Id);
                profile.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            foreach (var user in patient)
            {
                var profile = await _db.PatientProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                profile.CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff");
            }
            await _db.SaveChangesAsync();
            _logger.LogInformation("Seeded Users.");
        }
    }
    private async Task SeedWorkingCalendar()
    {
        _logger.LogInformation("Seeding Working Calendar data...");
        try
        {
            if (!_db.WorkingCalendars.Any())
            {
                var doctors = await _db.DoctorProfiles
                    .Where(d => d.IsActive && d.WorkingType != WorkingType.None)
                    .ToListAsync();

                var rooms = await _db.Rooms
                    .Where(r => r.Status)
                    .ToListAsync();

                if (!doctors.Any() || !rooms.Any())
                {
                    _logger.LogWarning("No active doctors or rooms found for seeding calendar");
                    return;
                }

                var allShifts = new List<(TimeSpan Start, TimeSpan End, string ShiftName)>
            {
                (new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0), "Morning"),
                (new TimeSpan(13, 0, 0), new TimeSpan(17, 0, 0), "Afternoon"),
                (new TimeSpan(18, 0, 0), new TimeSpan(22, 0, 0), "Evening")
            };
                var startDate = new DateOnly(2024, 3, 1);
                var endDate = new DateOnly(2024, 12, 31);

                foreach (var doctor in doctors)
                {
                    var room = rooms[doctors.IndexOf(doctor) % rooms.Count];

                    for (var date = startDate; date <= endDate; date = date.AddDays(1))
                    {
                        // Skip only Sundays
                        if (date.DayOfWeek != DayOfWeek.Sunday)
                        {
                            var c = new WorkingCalendar
                            {
                                DoctorID = doctor.Id,
                                RoomID = room.Id,
                                Date = date,
                                Status = WorkingStatus.Accept,
                                Note = $"Schedule for {doctor.DoctorId}"
                            };
                            var calendar = _db.WorkingCalendars.Add(c).Entity;

                            // Xác định ca làm việc dựa trên WorkingType
                            var shiftsForDoctor = GetShiftsBasedOnWorkingType(doctor.WorkingType, allShifts, date.DayOfWeek);

                            foreach (var shift in shiftsForDoctor)
                            {
                                _db.TimeWorkings.Add(new TimeWorking
                                {
                                    CalendarID = calendar.Id,
                                    StartTime = shift.Start,
                                    EndTime = shift.End,
                                    IsActive = true
                                });
                            }
                        }
                    }
                }
                await _db.SaveChangesAsync();

                _logger.LogInformation($"Successfully seeded calendars and time slots");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while seeding working calendar data");
            throw;
        }
    }

    private List<(TimeSpan Start, TimeSpan End, string ShiftName)> GetShiftsBasedOnWorkingType(
        WorkingType workingType,
        List<(TimeSpan Start, TimeSpan End, string ShiftName)> allShifts,
        DayOfWeek dayOfWeek)
    {
        var shiftsToAssign = new List<(TimeSpan Start, TimeSpan End, string ShiftName)>();

        switch (workingType)
        {
            case WorkingType.FullTime:
                shiftsToAssign.Add(allShifts[0]);
                shiftsToAssign.Add(allShifts[1]);
                break;

            case WorkingType.PartTime:
                shiftsToAssign.Add(allShifts[2]);
                break;

            default:
                break;
        }

        return shiftsToAssign;
    }
    private async Task SeedPatientDemoAsync()
    {
        if (_db.Users.Count() <= 5)
        {
            //patient demo 1
            var patientDemo1 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Patient,
                Email = "patient1@demo.com",
                UserName = "patient1.demo",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "patient1@demo.com"?.ToUpperInvariant(),
                NormalizedUserName = "patient1.demo".ToUpperInvariant(),
                IsActive = true,
                PhoneNumber = "0987654321",
            };

            _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
            var password = new PasswordHasher<ApplicationUser>();
            patientDemo1.PasswordHash = password.HashPassword(patientDemo1, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(patientDemo1);

            //patient demo 2
            var patientDemo2 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Patient,
                Email = "patient2@demo.com",
                UserName = "patient2.demo",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "patient2@demo.com"?.ToUpperInvariant(),
                NormalizedUserName = "patient2.demo".ToUpperInvariant(),
                IsActive = true,
                PhoneNumber = "0987654320"
            };

            _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
            patientDemo2.PasswordHash = password.HashPassword(patientDemo2, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(patientDemo2);

            //patient demo 3
            var patientDemo3 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Patient,
                Email = "patient3@demo.com",
                UserName = "patient3.demo",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "patient3@demo.com"?.ToUpperInvariant(),
                NormalizedUserName = "patient3.demo".ToUpperInvariant(),
                IsActive = true,
                PhoneNumber = "0987654322"
            };

            _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
            patientDemo3.PasswordHash = password.HashPassword(patientDemo3, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(patientDemo3);

            //patient demo 4
            var patientDemo4 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Patient,
                Email = "patient4@demo.com",
                UserName = "patient4.demo",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "patient4@demo.com"?.ToUpperInvariant(),
                NormalizedUserName = "patient4.demo".ToUpperInvariant(),
                IsActive = true,
                PhoneNumber = "0987654323"
            };

            _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
            patientDemo4.PasswordHash = password.HashPassword(patientDemo4, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(patientDemo4);

            //patient demo 5
            var patientDemo5 = new ApplicationUser
            {
                FirstName = _currentTenant.Id.Trim().ToLowerInvariant(),
                LastName = FSHRoles.Patient,
                Email = "patient5@demo.com",
                UserName = "patient5.demo",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                NormalizedEmail = "patient5@demo.com"?.ToUpperInvariant(),
                NormalizedUserName = "patient5.demo".ToUpperInvariant(),
                IsActive = true,
                PhoneNumber = "0987654324"
            };

            _logger.LogInformation("Seeding Default patient User for '{tenantId}' Tenant.", _currentTenant.Id);
            patientDemo5.PasswordHash = password.HashPassword(patientDemo5, MultitenancyConstants.DefaultPassword);
            await _userManager.CreateAsync(patientDemo5);
            // Assign role to user
            if (!await _userManager.IsInRoleAsync(patientDemo1, FSHRoles.Patient))
            {
                _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
                await _userManager.AddToRoleAsync(patientDemo1, FSHRoles.Patient);
                var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
                string code = "BN";
                if (amountP.Count() < 10)
                {
                    code += $"00{amountP.Count()}";
                }
                else if (amountP.Count() < 100)
                {
                    code += $"0{amountP.Count()}";
                }
                else
                {
                    code += $"{amountP.Count()}";
                }
                var pa = await _userManager.FindByEmailAsync(patientDemo1.Email);
                await _db.PatientProfiles.AddAsync(new PatientProfile
                {
                    UserId = pa.Id,
                    PatientCode = code,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                });
                _db.SaveChanges();
            }
            if (!await _userManager.IsInRoleAsync(patientDemo2, FSHRoles.Patient))
            {
                _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
                await _userManager.AddToRoleAsync(patientDemo2, FSHRoles.Patient);
                var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
                string code = "BN";
                if (amountP.Count() < 10)
                {
                    code += $"00{amountP.Count()}";
                }
                else if (amountP.Count() < 100)
                {
                    code += $"0{amountP.Count()}";
                }
                else
                {
                    code += $"{amountP.Count()}";
                }
                var pa = await _userManager.FindByEmailAsync(patientDemo2.Email);
                await _db.PatientProfiles.AddAsync(new PatientProfile
                {
                    UserId = pa.Id,
                    PatientCode = code,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                });
                _db.SaveChanges();
            }
            if (!await _userManager.IsInRoleAsync(patientDemo3, FSHRoles.Patient))
            {
                _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
                await _userManager.AddToRoleAsync(patientDemo3, FSHRoles.Patient);
                var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
                string code = "BN";
                if (amountP.Count() < 10)
                {
                    code += $"00{amountP.Count()}";
                }
                else if (amountP.Count() < 100)
                {
                    code += $"0{amountP.Count()}";
                }
                else
                {
                    code += $"{amountP.Count()}";
                }
                var pa = await _userManager.FindByEmailAsync(patientDemo3.Email);
                await _db.PatientProfiles.AddAsync(new PatientProfile
                {
                    UserId = pa.Id,
                    PatientCode = code,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                });
                _db.SaveChanges();
            }
            if (!await _userManager.IsInRoleAsync(patientDemo4, FSHRoles.Patient))
            {
                _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
                await _userManager.AddToRoleAsync(patientDemo4, FSHRoles.Patient);
                var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
                string code = "BN";
                if (amountP.Count() < 10)
                {
                    code += $"00{amountP.Count()}";
                }
                else if (amountP.Count() < 100)
                {
                    code += $"0{amountP.Count()}";
                }
                else
                {
                    code += $"{amountP.Count()}";
                }
                var pa = await _userManager.FindByEmailAsync(patientDemo4.Email);
                await _db.PatientProfiles.AddAsync(new PatientProfile
                {
                    UserId = pa.Id,
                    PatientCode = code,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                });
                _db.SaveChanges();
            }
            if (!await _userManager.IsInRoleAsync(patientDemo5, FSHRoles.Patient))
            {
                _logger.LogInformation("Assigning Basic Role to Admin 2 User for '{tenantId}' Tenant.", _currentTenant.Id);
                await _userManager.AddToRoleAsync(patientDemo5, FSHRoles.Patient);
                var amountP = await _userManager.GetUsersInRoleAsync(FSHRoles.Patient);
                string code = "BN";
                if (amountP.Count() < 10)
                {
                    code += $"00{amountP.Count()}";
                }
                else if (amountP.Count() < 100)
                {
                    code += $"0{amountP.Count()}";
                }
                else
                {
                    code += $"{amountP.Count()}";
                }
                var pa = await _userManager.FindByEmailAsync(patientDemo5.Email);
                await _db.PatientProfiles.AddAsync(new PatientProfile
                {
                    UserId = pa.Id,
                    PatientCode = code,
                    CreatedBy = Guid.Parse("f56b04ea-d95d-4fab-be50-2fd2ca1561ff")
                });
                _db.SaveChanges();
            }
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