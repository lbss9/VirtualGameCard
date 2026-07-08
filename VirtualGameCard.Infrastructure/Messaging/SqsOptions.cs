namespace VirtualGameCard.Infrastructure.Messaging;

public sealed class SqsOptions
{
    public bool Enabled { get; init; }
    public string Region { get; init; } = "us-east-2";
    public string PaymentRequestedQueueUrl { get; init; } = string.Empty;
    public string PaymentApprovedQueueUrl { get; init; } = string.Empty;
    public int WaitTimeSeconds { get; init; } = 20;
    public int VisibilityTimeoutSeconds { get; init; } = 180;
    public int MaxNumberOfMessages { get; init; } = 5;
    public int PaymentProcessingDelaySeconds { get; init; } = 30;
}
