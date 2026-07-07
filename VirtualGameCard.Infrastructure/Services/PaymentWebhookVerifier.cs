using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using VirtualGameCard.Application.Interfaces;

namespace VirtualGameCard.Infrastructure.Services;

public sealed class PaymentWebhookVerifier(IConfiguration configuration) : IPaymentWebhookVerifier
{
    public bool IsValid(string payload, string signature)
    {
        var secret = configuration["PaymentWebhook:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
            return false;
        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(payload)
        );
        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }
        return supplied.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
