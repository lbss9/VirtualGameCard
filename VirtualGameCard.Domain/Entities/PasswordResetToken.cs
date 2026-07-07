namespace VirtualGameCard.Domain.Entities;

/// <summary>
/// Token de redefinição de senha. Guardamos apenas o HASH do token — o valor
/// bruto é enviado ao usuário (por e-mail em produção) e nunca persiste.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;

    public bool IsActive => UsedAt is null && ExpiresAt > DateTime.UtcNow;
}
