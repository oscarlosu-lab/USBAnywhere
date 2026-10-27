using System;
using System.Diagnostics;
using Microsoft.Toolkit.Uwp.Notifications;
using USBAnywhere.Services.Interfaces;
using Forms = System.Windows.Forms;

namespace USBAnywhere.Services;

/// <summary>
/// Modern Windows Toast Notification Service with graceful fallback to system tray balloon tips
/// </summary>
public class NotificationService : INotificationService
{
    private Forms.NotifyIcon? _fallbackIcon;

    public void SetFallbackIcon(Forms.NotifyIcon? icon)
    {
        _fallbackIcon = icon;
    }

    public void ShowNotification(string title, string message, NotificationType type = NotificationType.Info)
    {
        try
        {
            var iconPrefix = type switch
            {
                NotificationType.Success => "✅ ",
                NotificationType.Warning => "⚠️ ",
                NotificationType.Error => "❌ ",
                _ => "ℹ️ "
            };

            new ToastContentBuilder()
                .AddText($"{iconPrefix}{title}")
                .AddText(message)
                .AddAttributionText("USBAnywhere v3.0")
                .Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Toast] Failed to show Windows toast: {ex.Message}. Falling back to Tray BalloonTip.");
            try
            {
                var tooltipIcon = type switch
                {
                    NotificationType.Warning => Forms.ToolTipIcon.Warning,
                    NotificationType.Error => Forms.ToolTipIcon.Error,
                    _ => Forms.ToolTipIcon.Info
                };
                _fallbackIcon?.ShowBalloonTip(3000, title, message, tooltipIcon);
            }
            catch
            {
                // Ignore secondary failure
            }
        }
    }
}
