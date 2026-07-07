namespace VirtualGameCard.Application.Interfaces;

public interface IPaymentWebhookVerifier
{
    bool IsValid(string payload, string signature);
}
