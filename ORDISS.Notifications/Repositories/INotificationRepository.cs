namespace ORDISS.Notifications.Repositories;

using ORDISS.Notifications.Models;

public interface INotificationRepository
{
    /// <summary>
    /// Get unread notifications for a user after a specific notification ID.
    /// </summary>
    Task<List<Notification>> GetNewNotificationsAsync(
        string userId,
        int? lastNotificationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark notifications as read.
    /// </summary>
    Task MarkAsReadAsync(
        string userId,
        List<int> notificationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new notification (for testing/demo purposes).
    /// </summary>
    Task AddNotificationAsync(
        Notification notification,
        CancellationToken cancellationToken = default);
}
