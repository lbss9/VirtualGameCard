using Microsoft.EntityFrameworkCore;
using VirtualGameCard.Domain.Entities;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Infrastructure.Repositories;

public sealed class NotificationRepository(AppDbContext db) : INotificationRepository
{
    public async Task AddAsync(Notification item)
    {
        db.Notifications.Add(item);
        await db.SaveChangesAsync();
    }

    public Task<List<Notification>> GetRecentAsync(Guid userId, int limit) =>
        db
            .Notifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public Task<int> CountUnreadAsync(Guid userId) =>
        db.Notifications.CountAsync(x => x.UserId == userId && !x.Read);

    public Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId) =>
        db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

    public async Task MarkAllReadAsync(Guid userId)
    {
        var items = await db.Notifications.Where(x => x.UserId == userId && !x.Read).ToListAsync();
        foreach (var item in items)
            item.Read = true;
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Notification item)
    {
        db.Notifications.Update(item);
        await db.SaveChangesAsync();
    }
}
