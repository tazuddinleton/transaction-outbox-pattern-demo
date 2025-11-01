using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using TransactionOutboxDemo.Db;

namespace TransactionOutboxDemo.Services;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly IConnection _rabbitMqConnection;
    private readonly IModel _channel;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public OutboxProcessorService(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessorService> logger,
        IConnection rabbitMqConnection)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _rabbitMqConnection = rabbitMqConnection;
        _channel = _rabbitMqConnection.CreateModel();
        
        // Declare exchange for domain events
        _channel.ExchangeDeclare(
            exchange: "domain-events",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false);
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

    private Task PublishMessageAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var routingKey = message.EventType.ToLowerInvariant();
        var body = Encoding.UTF8.GetBytes(message.Payload);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = message.Id.ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        properties.Type = message.EventType;

        _channel.BasicPublish(
            exchange: "domain-events",
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _rabbitMqConnection?.Close();
        _rabbitMqConnection?.Dispose();
        base.Dispose();
    }
}
