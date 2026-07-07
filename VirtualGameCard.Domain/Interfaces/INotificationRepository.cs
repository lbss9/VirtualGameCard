using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Domain.Interfaces;

public interface INotificationRepository
{
    Task AddAsync(Notification notification);
    Task<List<Notification>> GetRecentAsync(Guid userId, int limit);
    Task<int> CountUnreadAsync(Guid userId);
    Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId);
    Task MarkAllReadAsync(Guid userId);
    Task UpdateAsync(Notification notification);
}
