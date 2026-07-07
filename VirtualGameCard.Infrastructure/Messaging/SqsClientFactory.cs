using Amazon;
using Amazon.SQS;

namespace VirtualGameCard.Infrastructure.Messaging;

internal static class SqsClientFactory
{
    public static IAmazonSQS Create(SqsOptions options)
    {
        var region = RegionEndpoint.GetBySystemName(options.Region);
        return new AmazonSQSClient(region);
    }
}
