using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace VirtualGameCard.Api.Common;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var isConflict = exception is DbUpdateException;
        if (isConflict)
            logger.LogWarning("Conflito de persistência em {Path}", http.Request.Path);
        else
            logger.LogError(exception, "Erro não tratado em {Path}", http.Request.Path);
        var statusCode = isConflict
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;
        var error = ApiResponse.Failure(
            isConflict
                ? "A operação entrou em conflito com um recurso existente."
                : "Ocorreu um erro interno. Tente novamente mais tarde.",
            isConflict ? "RESOURCE_CONFLICT" : "INTERNAL_ERROR",
            statusCode,
            http.Request.Path
        );

        http.Response.StatusCode = statusCode;
        await http.Response.WriteAsJsonAsync(error, cancellationToken);

        return true;
    }
}
