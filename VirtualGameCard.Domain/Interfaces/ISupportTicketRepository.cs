using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface ISupportTicketRepository
{
    Task AddAsync(SupportTicket ticket);
}
