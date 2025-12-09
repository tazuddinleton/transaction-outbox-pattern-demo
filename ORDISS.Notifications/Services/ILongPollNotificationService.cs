namespace ORDISS.Notifications.Services;

using ORDISS.Notifications.Models;

/// <summary>
/// Orchestrates the long-polling flow:
/// 1. Check repository for existing notifications.
/// 2. If found, return immediately.
/// 3. If not found, wait on the event bus with a timeout.
/// 4. Return results to the endpoint.
/// </summary>
public interface ILongPollNotificationService
{
    Task<NotificationResponse> WaitForNotificationsAsync(
        string userId,
        int? lastNotificationId,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
