using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Api.Observability;
using VirtualGameCard.Application.Auth.Commands;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Domain.Interfaces;

namespace VirtualGameCard.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    RegisterCommandHandler registerHandler,
    LoginCommandHandler loginHandler,
    ForgotPasswordCommandHandler forgotPasswordHandler,
    ResetPasswordCommandHandler resetPasswordHandler,
    RefreshSessionCommandHandler refreshHandler,
    LogoutCommandHandler logoutHandler,
    IWebHostEnvironment env,
    VirtualGameCard.Application.Profile.VerifyEmailCommandHandler verifyEmailHandler
) : ControllerBase
{
    public record AuthRequest(string Email, string Password);

    [EnableRateLimiting("auth"), HttpPost("register")]
    [ProducesResponseType(
        typeof(ApiResponse<VirtualGameCard.Application.Auth.DTOs.AuthResponse>),
        StatusCodes.Status201Created
    )]
    public async Task<IActionResult> Register(AuthRequest request)
    {
        var result = await registerHandler.HandleAsync(
            new RegisterCommand(request.Email, request.Password)
        );
        AppMetrics
            .AuthEvents.WithLabels("register", result.IsSuccess ? "success" : "failure")
            .Inc();

        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);

        SetRefreshCookie(result.Value!);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse.Success(
                HttpContext,
                result.Value!.Response,
                "Conta criada com sucesso.",
                "REGISTER_SUCCESS",
                StatusCodes.Status201Created
            )
        );
    }

    [EnableRateLimiting("auth"), HttpPost("login")]
    [ProducesResponseType(
        typeof(ApiResponse<VirtualGameCard.Application.Auth.DTOs.AuthResponse>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Login(AuthRequest request)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password)
        );
        AppMetrics.AuthEvents.WithLabels("login", result.IsSuccess ? "success" : "failure").Inc();

        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);

        SetRefreshCookie(result.Value!);
        return Ok(
            ApiResponse.Success(
                HttpContext,
                result.Value!.Response,
                "Login realizado com sucesso.",
                "LOGIN_SUCCESS"
            )
        );
    }

    [EnableRateLimiting("auth"), HttpPost("refresh")]
    [ProducesResponseType(
        typeof(ApiResponse<VirtualGameCard.Application.Auth.DTOs.AuthResponse>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue("vgc_refresh", out var refreshToken);
        var result = await refreshHandler.HandleAsync(
            new RefreshSessionCommand(refreshToken ?? string.Empty),
            cancellationToken
        );
        if (!result.IsSuccess)
        {
            DeleteRefreshCookie();
            return result.Error!.ToActionResult(HttpContext);
        }
        SetRefreshCookie(result.Value!);
        return Ok(
            ApiResponse.Success(
                HttpContext,
                result.Value!.Response,
                "Sessão renovada com sucesso.",
                "SESSION_REFRESHED"
            )
        );
    }

    [Authorize, HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue("vgc_refresh", out var refreshToken);
        if (!Guid.TryParse(User.FindFirst("sid")?.Value, out var sessionId))
            return ApiResponse
                .Failure("Sessão inválida.", "SESSION_INVALID", 401, Request.Path)
                .AsResult(401);
        await logoutHandler.HandleAsync(
            new LogoutCommand(sessionId, refreshToken),
            cancellationToken
        );
        DeleteRefreshCookie();
        return NoContent();
    }

    public record ForgotPasswordRequest(string Email);

    [EnableRateLimiting("sensitive"), HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<ForgotPasswordData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await forgotPasswordHandler.HandleAsync(
            new ForgotPasswordCommand(request.Email)
        );

        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);

        // Resposta genérica: nunca revelamos se o e-mail existe.
        // Em desenvolvimento devolvemos o token para permitir testar o fluxo
        // sem um serviço de e-mail; em produção ele só seria enviado por e-mail.
        var response = new ForgotPasswordData(
            "Se o e-mail existir, enviamos instruções para redefinir a senha.",
            env.IsDevelopment() ? result.Value!.ResetToken : null,
            env.IsDevelopment() ? result.Value!.ExpiresAt : null
        );

        return Ok(
            ApiResponse.Success(
                HttpContext,
                response,
                "Se o e-mail existir, enviamos instruções para redefinir a senha.",
                "PASSWORD_RESET_REQUESTED"
            )
        );
    }

    public record ResetPasswordRequest(string Token, string NewPassword);

    [EnableRateLimiting("sensitive"), HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<MessageData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await resetPasswordHandler.HandleAsync(
            new ResetPasswordCommand(request.Token, request.NewPassword)
        );
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);

        var response = new MessageData("Senha redefinida com sucesso. Você já pode entrar.");
        return Ok(
            ApiResponse.Success(HttpContext, response, response.Message, "PASSWORD_RESET_SUCCESS")
        );
    }

    public record VerifyEmailRequest(string Token);

    [EnableRateLimiting("sensitive"), HttpPost("verify-email")]
    [ProducesResponseType(typeof(ApiResponse<MessageData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var result = await verifyEmailHandler.HandleAsync(
            new VirtualGameCard.Application.Profile.VerifyEmailCommand(request.Token)
        );
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        return Ok(
            ApiResponse.Success(
                HttpContext,
                new MessageData("E-mail confirmado com sucesso."),
                "E-mail confirmado com sucesso.",
                "EMAIL_VERIFIED"
            )
        );
    }

    private void SetRefreshCookie(VirtualGameCard.Application.Auth.DTOs.AuthSessionResult session)
    {
        Response.Cookies.Append(
            "vgc_refresh",
            session.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = env.IsDevelopment() ? SameSiteMode.Strict : SameSiteMode.None,
                Expires = new DateTimeOffset(session.RefreshTokenExpiresAt, TimeSpan.Zero),
                Path = "/api/auth",
                IsEssential = true,
            }
        );
    }

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(
            "vgc_refresh",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = env.IsDevelopment() ? SameSiteMode.Strict : SameSiteMode.None,
                Path = "/api/auth",
            }
        );
}
