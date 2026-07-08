using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Purchases.Messages;

namespace VirtualGameCard.Infrastructure.Messaging;

public sealed class SqsPaymentMessagePublisher(
    IOptions<SqsOptions> options,
    ILogger<SqsPaymentMessagePublisher> logger
) : IPaymentMessagePublisher
{
    private readonly SqsOptions _options = options.Value;

    public async Task PublishPaymentRequestedAsync(
        PaymentRequestedMessage message,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(_options.PaymentRequestedQueueUrl))
            throw new InvalidOperationException("Configure Sqs:PaymentRequestedQueueUrl.");

        using var sqs = SqsClientFactory.Create(_options);

        await sqs.SendMessageAsync(
            new SendMessageRequest
            {
                QueueUrl = _options.PaymentRequestedQueueUrl,
                MessageBody = JsonSerializer.Serialize(message),
                DelaySeconds = Math.Clamp(_options.PaymentProcessingDelaySeconds, 0, 900),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["messageType"] = new()
                    {
                        DataType = "String",
                        StringValue = nameof(PaymentRequestedMessage),
                    },
                    ["idempotencyKey"] = new()
                    {
                        DataType = "String",
                        StringValue = message.IdempotencyKey,
                    },
                },
            },
            cancellationToken
        );

        logger.LogInformation(
            "Mensagem de pagamento publicada no SQS. PurchaseId: {PurchaseId}, IdempotencyKey: {IdempotencyKey}",
            message.PurchaseId,
            message.IdempotencyKey
        );
    }
}
