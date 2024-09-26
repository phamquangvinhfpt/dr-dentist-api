using FSH.WebApi.Application.Common.Interfaces;
using FSH.WebApi.Domain.Notification;
using FSH.WebApi.Infrastructure.Persistence.Context;
using FSH.WebApi.Infrastructure.Persistence.Initialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace FSH.WebApi.Infrastructure.Notifications;
public class NotificationSeeder : ICustomSeeder
{
    private readonly ISerializerService _serializerService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotificationSeeder> _logger;

    public NotificationSeeder(ISerializerService serializerService, ApplicationDbContext db, ILogger<NotificationSeeder> logger)
    {
        _serializerService = serializerService;
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string dataPath = Path.Combine(path!, "Notifications", "NotificationData.json");
        if (!_db.Notifications.Any())
        {
            _logger.LogInformation("Started to Seed Notifications.");
            string notificationData = await File.ReadAllTextAsync(dataPath, cancellationToken);
            var notifications = _serializerService.Deserialize<List<Notification>>(notificationData);
            var users = await _db.Users.Where(u => u.UserName == "root.admin").FirstOrDefaultAsync();
            foreach (var notification in notifications)
            {
                notification.UserId = Guid.Parse(users.Id);
                _ = _db.Notifications.Add(notification);
            }

            _ = await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded Notifications.");
        }
    }
}
