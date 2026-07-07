using System.Security.Cryptography;

namespace VirtualGameCard.Application.Common;

/// <summary>Gera códigos de gift card no formato XXXX-XXXX-XXXX-XXXX.</summary>
public static class GiftCardCode
{
    // Sem 0/O/1/I para evitar confusão na hora de digitar o código.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var groups = new string[4];
        for (var g = 0; g < 4; g++)
        {
            var chars = new char[4];
            for (var i = 0; i < 4; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            groups[g] = new string(chars);
        }
        return string.Join('-', groups);
    }
}
