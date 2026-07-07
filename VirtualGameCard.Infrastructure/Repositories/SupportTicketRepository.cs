using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public sealed class SupportTicketRepository(AppDbContext db) : ISupportTicketRepository
{
    public async Task AddAsync(SupportTicket ticket)
    {
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync();
    }
}
