using FSH.WebApi.Application;
using FSH.WebApi.Host.Configurations;
using FSH.WebApi.Host.Controllers;
using FSH.WebApi.Infrastructure;
using FSH.WebApi.Infrastructure.BackgroundJobs;
using FSH.WebApi.Infrastructure.Common;
using FSH.WebApi.Infrastructure.Common.Services;
using FSH.WebApi.Infrastructure.Filters;
using FSH.WebApi.Infrastructure.Logging.Serilog;
using Hangfire;
using Serilog;
using Serilog.Formatting.Compact;

[assembly: ApiConventionType(typeof(FSHApiConventions))]

StaticLogger.EnsureInitialized();
Log.Information("Server Booting Up...");
try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddConfigurations().RegisterSerilog();
    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<BanFilter>();
    });
    builder.Services.AddInfrastructure(builder.Configuration);
    //builder.Services.AddScoped<BanFilter>();
    builder.Services.AddApplication();

    var app = builder.Build();

    await app.Services.InitializeDatabasesAsync();

    app.UseInfrastructure(builder.Configuration);

    RecurringJob.AddOrUpdate<AppointmentJobDaily>("AppointmentJobDaily", job => job.AppointmentJobDailyAsync(), Cron.Daily);

    //if (!app.Environment.IsDevelopment())
    app.MapEndpoints();
    app.Run();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    StaticLogger.EnsureInitialized();
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    StaticLogger.EnsureInitialized();
    Log.Information("Server Shutting down...");
    Log.CloseAndFlush();
}