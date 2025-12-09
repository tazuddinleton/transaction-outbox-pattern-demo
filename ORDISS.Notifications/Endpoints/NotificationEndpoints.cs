namespace ORDISS.Notifications.Endpoints;

using ORDISS.Notifications.Models;
using ORDISS.Notifications.Repositories;
using ORDISS.Notifications.Services;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications")
            .WithName("Notifications")
            .WithOpenApi();

        group.MapGet("/longpoll", GetLongPoll)
            .WithName("LongPollNotifications")
            .WithDescription("Long-polls for new notifications with a 30-second timeout")
            .WithOpenApi();

        group.MapPost("/test/notify", PostTestNotify)
            .WithName("TestNotify")
            .WithDescription("(Test endpoint) Simulate a new notification")
            .WithOpenApi();
    }

    /// <summary>
    /// Long-polling endpoint.
    /// GET /api/notifications/longpoll?lastNotificationId=5
    /// </summary>
    private static async Task<IResult> GetLongPoll(
        ILongPollNotificationService service,
        HttpContext context,
        int? lastNotificationId = null)
    {
        // Extract userId from claims or use a test value.
        var userId = context.User.FindFirst("sub")?.Value 
            ?? context.User.FindFirst("nameidentifier")?.Value 
            ?? "test-user";

        const int timeoutSeconds = 30;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        try
        {
            var response = await service.WaitForNotificationsAsync(
                userId,
                lastNotificationId,
                timeout,
                context.RequestAborted)
                .ConfigureAwait(false);

            return Results.Ok(response);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected or aborted.
            return Results.StatusCode(499); // Client Closed Request
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message, statusCode: 500);
        }
    }

    /// <summary>
    /// Test endpoint to simulate publishing a notification.
    /// POST /api/notifications/test/notify
    /// Body: { "userId": "test-user", "title": "Test", "body": "This is a test" }
    /// </summary>
    private static async Task<IResult> PostTestNotify(
        INotificationRepository repository,
        NotificationEventBus eventBus,
        TestNotifyRequest request)
    {
        var notification = new Notification
        {
            UserId = request.UserId,
            Title = request.Title,
            Body = request.Body,
            IsRead = false
        };

        await repository.AddNotificationAsync(notification)
            .ConfigureAwait(false);

        eventBus.PublishNotification(request.UserId);

        return Results.Ok(new { message = "Notification published", notificationId = notification.Id });
    }
}

public class TestNotifyRequest
{
    public string UserId { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
}
