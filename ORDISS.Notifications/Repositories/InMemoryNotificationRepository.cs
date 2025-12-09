namespace ORDISS.Notifications.Repositories;

using ORDISS.Notifications.Models;

/// <summary>
/// In-memory repository for demonstration.
/// In production, replace with a database (SQL Server, PostgreSQL, etc.).
/// </summary>
public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task<List<Notification>> GetNewNotificationsAsync(
        string userId,
        int? lastNotificationId = null,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var query = _notifications
                .Where(n => n.UserId == userId && !n.IsRead);

            if (lastNotificationId.HasValue)
            {
                query = query.Where(n => n.Id > lastNotificationId.Value);
            }

            return Task.FromResult(query.OrderBy(n => n.Id).ToList());
        }
    }

    public Task MarkAsReadAsync(
        string userId,
        List<int> notificationIds,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            foreach (var id in notificationIds)
            {
                var notification = _notifications.FirstOrDefault(n => n.Id == id && n.UserId == userId);
                if (notification != null)
                {
                    notification.IsRead = true;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task AddNotificationAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            notification.Id = _nextId++;
            notification.CreatedAt = DateTime.UtcNow;
            _notifications.Add(notification);
        }

        return Task.CompletedTask;
    }
}
