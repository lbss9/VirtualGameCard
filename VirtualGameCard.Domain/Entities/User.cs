namespace VirtualGameCard.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsAccountVerified { get; set; } = false;
    public DateTime? AccountVerifiedAt { get; set; } = null;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];
    public ICollection<GiftCardPurchase> Purchases { get; set; } = [];
    public ICollection<EmailVerificationToken> EmailVerificationTokens { get; set; } = [];
    public ICollection<SupportTicket> SupportTickets { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<RefreshSession> RefreshSessions { get; set; } = [];
}
