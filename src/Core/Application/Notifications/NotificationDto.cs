﻿using static FSH.WebApi.Shared.Notifications.BasicNotification;

namespace FSH.WebApi.Application.Notifications;
public class NotificationDto : IDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public LabelType Label { get; set; }
    public string Message { get; set; }
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedOn { get; set; }
}
