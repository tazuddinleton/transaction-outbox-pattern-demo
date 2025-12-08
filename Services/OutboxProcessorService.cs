using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using TransactionOutboxDemo.Db;
using TransactionOutboxDemo.Domain.Events;

namespace TransactionOutboxDemo.Services;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorService> logger,
        IPublishEndpoint publishEndpoint)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Processor Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        // Get unprocessed messages (limit to 100 at a time)
        var messages = await context.OutboxMessages
            .Where(m => !m.Processed)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (!messages.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                await PublishMessageAsync(message, cancellationToken);

                // Mark as processed
                message.Processed = true;
                message.ProcessedAt = DateTime.UtcNow;
                
                _logger.LogInformation("Processed outbox message {MessageId} of type {EventType}", 
                    message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                // Don't mark as processed if publishing failed - will retry on next iteration
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        switch (message.EventType)
        {
            case nameof(OrderCreatedEvent):
                var orderCreated = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Payload, JsonOptions);
                if (orderCreated != null)
                {
                    await _publishEndpoint.Publish(orderCreated, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize {EventType} message {MessageId}", message.EventType, message.Id);
                }
                break;
            default:
                _logger.LogWarning("Unsupported event type {EventType} for message {MessageId}", message.EventType, message.Id);
                break;
        }
    }
}
