using USBAnywhere.Services;
using USBAnywhere.Services.Interfaces;
using Xunit;

namespace USBAnywhere.Tests;

public class NotificationServiceTests
{
    [Fact]
    public void NotificationService_DoesNotThrow_OnAnyNotificationType()
    {
        var service = new NotificationService();

        // Should execute smoothly and fallback safely without unhandled exception
        service.ShowNotification("Test Title", "Test Message", NotificationType.Info);
        service.ShowNotification("Test Success", "Connected", NotificationType.Success);
        service.ShowNotification("Test Warning", "Warning message", NotificationType.Warning);
        service.ShowNotification("Test Error", "Error message", NotificationType.Error);
    }
}
