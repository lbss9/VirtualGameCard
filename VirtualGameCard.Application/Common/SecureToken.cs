using System.Security.Cryptography;

namespace VirtualGameCard.Application.Common;

/// <summary>
/// Gera e valida tokens de uso único (redefinição de senha). O token bruto é
/// enviado ao usuário; no banco guardamos apenas o hash SHA-256, para que um
/// vazamento do banco não permita redefinir senhas.
/// </summary>
public static class SecureToken
{
    /// <summary>Gera um token bruto aleatório, seguro para URL.</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    /// <summary>Hash determinístico do token, para armazenar e comparar.</summary>
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
