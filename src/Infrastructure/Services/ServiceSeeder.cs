using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Infrastructure.Identity;
using FSH.WebApi.Infrastructure.Multitenancy;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.Services;
public class ServiceSeeder : ICustomSeeder
{
    private readonly ISerializerService _serializerService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ServiceSeeder> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FSHTenantInfo _currentTenant;

    public ServiceSeeder(ISerializerService serializerService,
        ApplicationDbContext db,
        ILogger<ServiceSeeder> logger,
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
        _logger.LogInformation("Started to Seed Users.");
        string userData = await File.ReadAllTextAsync(dataPath, cancellationToken);
        var users = _serializerService.Deserialize<List<ApplicationUser>>(userData);
    }
}
