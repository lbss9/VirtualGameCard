using VirtualGameCard.Domain.Entities;

namespace VirtualGameCard.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user, Guid sessionId);
}
