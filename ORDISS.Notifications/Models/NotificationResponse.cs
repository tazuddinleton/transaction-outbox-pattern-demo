namespace ORDISS.Notifications.Models;

public class NotificationResponse
{
    public List<Notification> Notifications { get; set; } = new();
    public bool TimedOut { get; set; }
    public DateTime? LastCheckedAt { get; set; }
}
