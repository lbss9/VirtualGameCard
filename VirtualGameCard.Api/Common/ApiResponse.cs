using Microsoft.AspNetCore.Mvc;

namespace VirtualGameCard.Api.Common;

/// <summary>Envelope único retornado por todos os endpoints da API.</summary>
public sealed record ApiResponse<T>(
    string Message,
    string Code,
    string Path,
    int StatusCode,
    T? Data
);

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(
        HttpContext http,
        T data,
        string message,
        string code,
        int statusCode = StatusCodes.Status200OK
    ) => new(message, code, http.Request.Path, statusCode, data);

    public static ApiResponse<object?> Failure(
        string message,
        string code,
        int statusCode,
        string path
    ) => new(message, code, path, statusCode, null);

    public static ApiResponse<object?> FromStatus(int statusCode, string path) =>
        statusCode switch
        {
            401 => Failure("Autenticação necessária.", "UNAUTHORIZED", statusCode, path),
            403 => Failure("Acesso negado.", "FORBIDDEN", statusCode, path),
            404 => Failure("Recurso não encontrado.", "NOT_FOUND", statusCode, path),
            405 => Failure("Método não permitido.", "METHOD_NOT_ALLOWED", statusCode, path),
            _ => Failure("Ocorreu um erro ao processar a requisição.", "ERROR", statusCode, path),
        };
}

public static class ApiResponseResultExtensions
{
    public static ObjectResult AsResult<T>(this ApiResponse<T> response, int statusCode) =>
        new(response) { StatusCode = statusCode };
}
