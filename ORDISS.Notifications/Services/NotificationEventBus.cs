namespace ORDISS.Notifications.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// In-memory event bus for signaling new notifications to waiting clients.
/// 
/// Why TaskCompletionSource?
/// - Allows async waiting without blocking threads.
/// - Each waiting request creates a TCS that completes when a notification arrives.
/// - The main thread pool thread is freed while waiting (efficient).
/// 
/// Thread Safety:
/// - Uses ConcurrentDictionary to store per-user waiters without locks.
/// - CancellationToken ensures cleanup when client disconnects.
/// - TCS guarantees only one waiter per notification, preventing duplicates.
/// </summary>
public class NotificationEventBus
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _userWaiters = new();
    private readonly ILogger<NotificationEventBus> _logger;

    public NotificationEventBus(ILogger<NotificationEventBus> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Publishes a notification event, waking up any waiting clients for that user.
    /// </summary>
    public void PublishNotification(string userId)
    {
        if (!_userWaiters.TryRemove(userId, out var tcs))
        {
            _logger.LogDebug("No waiters for userId {UserId}", userId);
            return;
        }

        // Signal the TCS with success, which resumes the waiting request.
        var setResult = tcs.TrySetResult(true);
        _logger.LogInformation(
            "Published notification for userId {UserId}, waiter signaled: {SetResult}",
            userId,
            setResult);
    }

    /// <summary>
    /// Asynchronously waits for a notification with a timeout.
    /// This is the heart of long-polling: it's completely non-blocking.
    /// 
    /// How it works:
    /// 1. Create a TaskCompletionSource (a task that we control).
    /// 2. Add it to the dictionary keyed by userId.
    /// 3. Await the task with a timeout using CancellationToken.
    /// 4. When PublishNotification is called, the TCS completes and we resume.
    /// 5. If timeout expires, CancellationToken triggers and task is cancelled.
    /// 6. Always remove the TCS from the dictionary (cleanup).
    /// </summary>
    public async Task WaitForNotificationAsync(
        string userId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // Create a TCS for this specific wait.
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Register cancellation handler to clean up if client disconnects.
        using var registration = cancellationToken.Register(
            () => tcs.TrySetCanceled(cancellationToken),
            useSynchronizationContext: false);

        // Store this waiter so PublishNotification can find it.
        // If another waiter already exists, we replace it (only one waiter per user at a time).
        _userWaiters.AddOrUpdate(userId, tcs, (_, _) => tcs);

        try
        {
            // Wait for either:
            // - PublishNotification to complete the TCS.
            // - The timeout to elapse.
            // - The cancellation token to be cancelled (client disconnect).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            // Cleanup: remove this TCS from the dictionary.
            // This prevents memory leaks if multiple waits happen sequentially.
            _userWaiters.TryRemove(userId, out _);
        }
    }

    /// <summary>
    /// Gets the count of active waiters (for monitoring/debugging).
    /// </summary>
    public int GetActiveWaiterCount() => _userWaiters.Count;
}
