using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Service;
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
        string dataPath = Path.Combine(path!, "Services", "ServiceData.json");
        if(_db.Services.Count() < 1)
        {
            _logger.LogInformation("Started to Seed Service.");
            string serviceData = await File.ReadAllTextAsync(dataPath, cancellationToken);
            var services = _serializerService.Deserialize<List<Service>>(serviceData);
            await _db.Services.AddRangeAsync(services, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            dataPath = Path.Combine(path!, "Services", "ProcedureData.json");
            string proceData = await File.ReadAllTextAsync(dataPath, cancellationToken);
            var procedures = _serializerService.Deserialize<List<Procedure>>(proceData);
            await _db.Procedures.AddRangeAsync(procedures, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            foreach (var service in services) {
                for (int i = 0; i < procedures.Count(); i++) {
                    _db.ServiceProcedures.Add(new ServiceProcedures
                    {
                        ServiceId = service.Id,
                        ProcedureId = procedures[i].Id,
                        StepOrder = i + 1,
                    });
                }
            }
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded Services.");
        }
    }
}
