using Finbuckle.MultiTenant;
using FSH.WebApi.Application.Appointments;
using FSH.WebApi.Infrastructure.Payments;
using FSH.WebApi.Shared.Multitenancy;
using Hangfire;
using Hangfire.Annotations;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace FSH.WebApi.Infrastructure.BackgroundJobs;
public class AppointmentJobDaily
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _webHost;
    public AppointmentJobDaily(IServiceScopeFactory scopeFactory, IWebHostEnvironment webHostEnvironment)
    {
        _scopeFactory = scopeFactory;
        _webHost = webHostEnvironment;
    }

    public async Task AppointmentJobDailyAsync()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var url = "";
            if (_webHost.IsDevelopment())
            {
                url = "https://localhost:5001/api/appointment/job";
            }
            else
            {
                url = "https://api.drdentist.me/api/appointment/job";
            }
            TransactionsUtils.CallAPIChecking(url);
        }
    }
}
