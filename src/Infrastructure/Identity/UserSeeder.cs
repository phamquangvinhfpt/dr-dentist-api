using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Infrastructure.Persistence.Initialization;
using FSH.WebApi.Shared.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace FSH.WebApi.Infrastructure.Identity;
public class UserSeeder : ICustomSeeder
{

    private readonly ISerializerService _serializerService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UserSeeder> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FSHTenantInfo _currentTenant;

    public UserSeeder(ISerializerService serializerService,
        ApplicationDbContext db,
        ILogger<UserSeeder> logger,
        UserManager<ApplicationUser> userManager,
        FSHTenantInfo currentTenant)
    {
        _serializerService = serializerService;
        _db = db;
        _logger = logger;
        _userManager = userManager;
        _currentTenant = currentTenant;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Identity", "UserData.json");
        if (_db.Users.Count() < 5)
        {
            _logger.LogInformation("Started to Seed Users.");
            string userData = await File.ReadAllTextAsync(dataPath, cancellationToken);
            var users = _serializerService.Deserialize<List<ApplicationUser>>(userData);
            List<ApplicationUser> doctor = new List<ApplicationUser>();
            List<ApplicationUser> staff = new List<ApplicationUser>();
            List<ApplicationUser> patient = new List<ApplicationUser>();
            int flash = 0;
            foreach (var user in users)
            {
                var entry = _db.Users.Add(user).Entity;
                if (flash < 5)
                {
                    doctor.Add(entry);
                    flash++;
                }
                else if (flash >= 5 && flash < 10)
                {
                    staff.Add(entry);
                    flash++;
                }
                else
                {
                    patient.Add(entry);
                }
            }
            _ = await _db.SaveChangesAsync(cancellationToken);
            foreach (var user in doctor)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Dentist))
                {
                    _logger.LogInformation("Assigning Dentist Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Dentist);
                }
            }
            foreach (var user in staff)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Staff))
                {
                    _logger.LogInformation("Assigning Staff Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Staff);
                }
            }
            foreach (var user in patient)
            {
                if (!await _userManager.IsInRoleAsync(user, FSHRoles.Patient))
                {
                    _logger.LogInformation("Assigning Patient Role to User for '{tenantId}' Tenant.", _currentTenant.Id);
                    await _userManager.AddToRoleAsync(user, FSHRoles.Patient);
                }
            }

            _logger.LogInformation("Seeded Users.");
        }
    }
}