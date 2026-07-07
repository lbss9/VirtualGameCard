namespace VirtualGameCard.Application.Auth.DTOs;

/// <summary>
/// Resultado interno do fluxo "esqueci a senha". O token bruto só é preenchido
/// quando um usuário com aquele e-mail existe; a camada de API decide se o
/// expõe (ambiente de desenvolvimento) ou se o envia por e-mail (produção).
/// </summary>
public record ForgotPasswordResult(string? ResetToken, DateTime? ExpiresAt);
