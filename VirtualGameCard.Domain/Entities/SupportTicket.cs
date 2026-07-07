namespace VirtualGameCard.Domain.Entities;

public enum SupportCategory
{
    Code,
    Payment,
    Account,
    Other,
}

public enum SupportTicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed,
}

public class SupportTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public SupportCategory Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
}
