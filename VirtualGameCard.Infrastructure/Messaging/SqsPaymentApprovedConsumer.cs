using System.Text.Json;
using Amazon.SQS.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualGameCard.Application.Purchases.Commands;
using VirtualGameCard.Application.Purchases.Messages;

namespace VirtualGameCard.Infrastructure.Messaging;

public sealed class SqsPaymentApprovedConsumer(
    IOptions<SqsOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<SqsPaymentApprovedConsumer> logger
) : BackgroundService
{
    private readonly SqsOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("SQS desativado. Consumer de pagamentos aprovados não iniciado.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.PaymentApprovedQueueUrl))
            throw new InvalidOperationException("Configure Sqs:PaymentApprovedQueueUrl.");

        using var sqs = SqsClientFactory.Create(_options);

        logger.LogInformation("Consumer SQS escutando pagamentos aprovados.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var response = await sqs.ReceiveMessageAsync(
                new ReceiveMessageRequest
                {
                    QueueUrl = _options.PaymentApprovedQueueUrl,
                    MaxNumberOfMessages = _options.MaxNumberOfMessages,
                    WaitTimeSeconds = _options.WaitTimeSeconds,
                    VisibilityTimeout = _options.VisibilityTimeoutSeconds,
                    MessageAttributeNames = ["All"],
                },
                stoppingToken
            );

            foreach (var message in response.Messages ?? [])
            {
                await ProcessMessageAsync(sqs, message, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        Amazon.SQS.IAmazonSQS sqs,
        Message sqsMessage,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var message = JsonSerializer.Deserialize<PaymentApprovedMessage>(
                sqsMessage.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (message is null || string.IsNullOrWhiteSpace(message.IdempotencyKey))
            {
                logger.LogWarning("Mensagem PaymentApproved inválida recebida.");
                await DeleteAsync(sqs, sqsMessage, cancellationToken);
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var handler =
                scope.ServiceProvider.GetRequiredService<ProcessPaymentApprovedMessageCommandHandler>();

            var result = await handler.HandleAsync(
                new ProcessPaymentApprovedMessageCommand(
                    message.PurchaseId,
                    message.PaymentId,
                    message.IdempotencyKey,
                    message.ApprovedAtUtc
                ),
                cancellationToken
            );

            if (!result.IsSuccess)
            {
                logger.LogWarning(
                    "Falha ao processar PaymentApproved. Code: {Code}, Message: {Message}",
                    result.Error!.Code,
                    result.Error.Message
                );
                return;
            }

            await DeleteAsync(sqs, sqsMessage, cancellationToken);

            logger.LogInformation(
                "PaymentApproved processado. PurchaseId: {PurchaseId}, PaymentId: {PaymentId}",
                message.PurchaseId,
                message.PaymentId
            );
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro ao processar PaymentApproved do SQS.");
        }
    }

    private Task DeleteAsync(
        Amazon.SQS.IAmazonSQS sqs,
        Message message,
        CancellationToken cancellationToken
    ) =>
        sqs.DeleteMessageAsync(
            _options.PaymentApprovedQueueUrl,
            message.ReceiptHandle,
            cancellationToken
        );
}
