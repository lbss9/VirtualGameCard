namespace VirtualGameCard.Application.Auth.DTOs;

public record AuthResponse(string Token, Guid UserId);
