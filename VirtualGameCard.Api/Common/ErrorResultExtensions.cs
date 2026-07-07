using Microsoft.AspNetCore.Mvc;
using VirtualGameCard.Application.Common;

namespace VirtualGameCard.Api.Common;

public static class ErrorResultExtensions
{
    /// <summary>
    /// Converte um <see cref="Error"/> de negócio no response HTTP padronizado,
    /// escolhendo o status code a partir da categoria do erro.
    /// </summary>
    public static IActionResult ToActionResult(this Error error, HttpContext http)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        var body = ApiResponse.Failure(error.Message, error.Code, statusCode, http.Request.Path);
        return new ObjectResult(body) { StatusCode = statusCode };
    }
}
