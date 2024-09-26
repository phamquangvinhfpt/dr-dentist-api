namespace FSH.WebApi.Shared.Notifications;

public class BasicNotification : INotificationMessage
{
    public enum LabelType
    {
        Information,
        Success,
        Warning,
        Error,
        Reminder
    }

    public string Title { get; set; }
    public string Message { get; set; }
    public LabelType Label { get; set; }
    public string? Url { get; set; }
}