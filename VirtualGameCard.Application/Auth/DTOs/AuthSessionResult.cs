namespace VirtualGameCard.Application.Auth.DTOs;

public sealed record AuthSessionResult(
    AuthResponse Response,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt
);
