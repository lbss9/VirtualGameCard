namespace VirtualGameCard.Application.Common;

/// <summary>
/// Categoria do erro. A camada de API traduz cada categoria para um HTTP status code.
/// </summary>
public enum ErrorType
{
    Validation, // 400
    Unauthorized, // 401
    NotFound, // 404
    Conflict, // 409
}

/// <summary>
/// Representa uma falha de negócio: um código estável para o cliente,
/// uma mensagem legível e a categoria que define o status HTTP.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string message, string code = "VALIDATION_ERROR") =>
        new(code, message, ErrorType.Validation);

    public static Error Unauthorized(string message, string code = "UNAUTHORIZED") =>
        new(code, message, ErrorType.Unauthorized);

    public static Error NotFound(string message, string code = "NOT_FOUND") =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string message, string code = "CONFLICT") =>
        new(code, message, ErrorType.Conflict);
}
