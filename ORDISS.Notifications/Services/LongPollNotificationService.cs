namespace ORDISS.Notifications.Services;

using ORDISS.Notifications.Models;
using ORDISS.Notifications.Repositories;
using Microsoft.Extensions.Logging;

public class LongPollNotificationService : ILongPollNotificationService
{
    private readonly INotificationRepository _repository;
    private readonly NotificationEventBus _eventBus;
    private readonly ILogger<LongPollNotificationService> _logger;

    public LongPollNotificationService(
        INotificationRepository repository,
        NotificationEventBus eventBus,
        ILogger<LongPollNotificationService> logger)
    {
        _repository = repository;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<NotificationResponse> WaitForNotificationsAsync(
        string userId,
        int? lastNotificationId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Long-poll request started for userId {UserId}, lastId {LastId}, timeout {Timeout}ms",
            userId,
            lastNotificationId,
            timeout.TotalMilliseconds);

        // Step 1: Check repository for existing notifications.
        var notifications = await _repository.GetNewNotificationsAsync(
            userId,
            lastNotificationId,
            cancellationToken)
            .ConfigureAwait(false);

        if (notifications.Count > 0)
        {
            _logger.LogInformation(
                "Found {Count} existing notifications for userId {UserId}, returning immediately",
                notifications.Count,
                userId);

            return new NotificationResponse
            {
                Notifications = notifications,
                TimedOut = false,
                LastCheckedAt = DateTime.UtcNow
            };
        }

        _logger.LogInformation(
            "No existing notifications for userId {UserId}, waiting for new ones...",
            userId);

        // Step 2: No notifications found. Wait on the event bus.
        // This is completely non-blocking.
        try
        {
            await _eventBus.WaitForNotificationAsync(userId, timeout, cancellationToken)
                .ConfigureAwait(false);

            // Step 3: A notification was published! Re-check the repository.
            notifications = await _repository.GetNewNotificationsAsync(
                userId,
                lastNotificationId,
                cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Notification arrived for userId {UserId}, returning {Count} notifications",
                userId,
                notifications.Count);

            return new NotificationResponse
            {
                Notifications = notifications,
                TimedOut = false,
                LastCheckedAt = DateTime.UtcNow
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected or request was aborted.
            _logger.LogInformation(
                "Long-poll request cancelled for userId {UserId} (client disconnect or abort)",
                userId);

            throw;
        }
        catch (OperationCanceledException)
        {
            // Timeout expired.
            _logger.LogInformation(
                "Long-poll timeout expired for userId {UserId}",
                userId);

            return new NotificationResponse
            {
                Notifications = new(),
                TimedOut = true,
                LastCheckedAt = DateTime.UtcNow
            };
        }
    }
}
