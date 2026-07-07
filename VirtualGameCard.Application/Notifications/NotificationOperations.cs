using VirtualGameCard.Application.Common;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Title,
    string Message,
    string Kind,
    DateTime CreatedAt,
    bool Read
);

public sealed record NotificationsResult(IReadOnlyList<NotificationDto> Items, int UnreadCount);

public sealed record GetNotificationsQuery;

public sealed record MarkNotificationReadCommand(Guid NotificationId);

public sealed record MarkAllNotificationsReadCommand;

public sealed class GetNotificationsQueryHandler(
    ICurrentUser current,
    INotificationRepository notifications
)
{
    public async Task<Result<NotificationsResult>> HandleAsync()
    {
        if (current.Id is not Guid userId)
            return Result<NotificationsResult>.Failure(
                Error.Unauthorized("Autenticação necessária.")
            );
        var items = await notifications.GetRecentAsync(userId, 30);
        var unread = await notifications.CountUnreadAsync(userId);
        return Result<NotificationsResult>.Success(
            new NotificationsResult(
                items
                    .Select(x => new NotificationDto(
                        x.Id,
                        x.Title,
                        x.Message,
                        x.Kind.ToString().ToLowerInvariant(),
                        x.CreatedAt,
                        x.Read
                    ))
                    .ToList(),
                unread
            )
        );
    }
}

public sealed class MarkNotificationReadCommandHandler(
    ICurrentUser current,
    INotificationRepository notifications
)
{
    public async Task<Result<bool>> HandleAsync(MarkNotificationReadCommand command)
    {
        if (current.Id is not Guid userId)
            return Result<bool>.Failure(Error.Unauthorized("Autenticação necessária."));
        var item = await notifications.GetByIdForUserAsync(command.NotificationId, userId);
        if (item is null)
            return Result<bool>.Failure(
                Error.NotFound("Notificação não encontrada.", "NOTIFICATION_NOT_FOUND")
            );
        item.Read = true;
        await notifications.UpdateAsync(item);
        return Result<bool>.Success(true);
    }
}

public sealed class MarkAllNotificationsReadCommandHandler(
    ICurrentUser current,
    INotificationRepository notifications
)
{
    public async Task<Result<bool>> HandleAsync()
    {
        if (current.Id is not Guid userId)
            return Result<bool>.Failure(Error.Unauthorized("Autenticação necessária."));
        await notifications.MarkAllReadAsync(userId);
        return Result<bool>.Success(true);
    }
}
