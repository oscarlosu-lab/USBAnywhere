namespace USBAnywhere.Services.Interfaces;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error
}

public interface INotificationService
{
    void ShowNotification(string title, string message, NotificationType type = NotificationType.Info);
}
