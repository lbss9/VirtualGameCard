using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Purchases.Messages;

namespace VirtualGameCard.Infrastructure.Messaging;

public sealed class RabbitMqPaymentMessagePublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPaymentMessagePublisher> logger
) : IPaymentMessagePublisher
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task PublishPaymentRequestedAsync(
        PaymentRequestedMessage message,
        CancellationToken cancellationToken = default
    )
    {
        var factory = CreateConnectionFactory();

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken
        );

        await channel.ExchangeDeclareAsync(
            exchange: _options.Exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken
        );

        await channel.QueueDeclareAsync(
            queue: _options.PaymentRequestedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken
        );

        await channel.QueueBindAsync(
            queue: _options.PaymentRequestedQueue,
            exchange: _options.Exchange,
            routingKey: _options.PaymentRequestedRoutingKey,
            cancellationToken: cancellationToken
        );

        var payload = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(payload);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.PurchaseId.ToString(),
            CorrelationId = message.IdempotencyKey,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        await channel.BasicPublishAsync(
            exchange: _options.Exchange,
            routingKey: _options.PaymentRequestedRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken
        );

        logger.LogInformation(
            "Mensagem de pagamento publicada. PurchaseId: {PurchaseId}, IdempotencyKey: {IdempotencyKey}",
            message.PurchaseId,
            message.IdempotencyKey
        );
    }

    private ConnectionFactory CreateConnectionFactory()
    {
        if (!string.IsNullOrWhiteSpace(_options.Uri))
        {
            return new ConnectionFactory { Uri = new Uri(_options.Uri) };
        }

        return new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
        };
    }
}
