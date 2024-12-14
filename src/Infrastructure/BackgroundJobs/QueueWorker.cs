using FSH.WebApi.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSH.WebApi.Infrastructure.BackgroundJobs;
public class QueueWorker : BackgroundService
{
    private readonly RequestQueue _requestQueue;
    private readonly ILogger<QueueWorker> _logger;
    private readonly RequestDelegate _next;

    public QueueWorker(RequestQueue requestQueue, ILogger<QueueWorker> logger, RequestDelegate next)
    {
        _requestQueue = requestQueue;
        _logger = logger;
        _next = next;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue Worker is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Queue Worker is running.");
            var context = _requestQueue.Dequeue();
            if (context != null)
            {
                try
                {
                    _logger.LogInformation($"Processing request: {context.Request.Path}");

                    await _next(context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request from queue.");
                }
            }
            else
            {
                await Task.Delay(1000);
            }
        }
    }
}
