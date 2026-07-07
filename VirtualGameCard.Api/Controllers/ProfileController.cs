using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Application.Profile;

namespace VirtualGameCard.Api.Controllers;

[Authorize, ApiController, Route("api/me")]
public sealed class ProfileController(
    GetProfileQueryHandler getProfile,
    ChangePasswordCommandHandler changePassword,
    SendEmailVerificationCommandHandler sendVerification,
    SimulateEmailVerificationCommandHandler simulateVerification,
    IWebHostEnvironment environment,
    IConfiguration configuration
) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<VirtualGameCard.Application.Profile.ProfileDto>),
        StatusCodes.Status200OK
    )]
    public async Task<IActionResult> GetMe()
    {
        var result = await getProfile.HandleAsync();
        return result.IsSuccess
            ? Ok(
                ApiResponse.Success(
                    HttpContext,
                    result.Value,
                    "Perfil carregado.",
                    "PROFILE_RETRIEVED"
                )
            )
            : result.Error!.ToActionResult(HttpContext);
    }

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [EnableRateLimiting("sensitive"), HttpPost("password")]
    [ProducesResponseType(typeof(ApiResponse<MessageData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await changePassword.HandleAsync(
            new ChangePasswordCommand(request.CurrentPassword, request.NewPassword),
            cancellationToken
        );
        return result.IsSuccess
            ? Ok(
                ApiResponse.Success(
                    HttpContext,
                    new MessageData("Senha alterada com sucesso."),
                    "Senha alterada com sucesso.",
                    "PASSWORD_CHANGED"
                )
            )
            : result.Error!.ToActionResult(HttpContext);
    }

    [EnableRateLimiting("sensitive"), HttpPost("email-verification")]
    [ProducesResponseType(typeof(ApiResponse<EmailVerificationSentData>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendVerification(CancellationToken cancellationToken)
    {
        var result = await sendVerification.HandleAsync(cancellationToken);
        if (!result.IsSuccess)
            return result.Error!.ToActionResult(HttpContext);
        return Ok(
            ApiResponse.Success(
                HttpContext,
                new EmailVerificationSentData(
                    "Enviamos um novo link de confirmação para o seu e-mail.",
                    environment.IsDevelopment() ? result.Value : null
                ),
                "Confirmação enviada.",
                "EMAIL_VERIFICATION_SENT"
            )
        );
    }

    [EnableRateLimiting("sensitive"), HttpPost("email-verification/simulate-confirm")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulateVerification()
    {
        var enabled =
            environment.IsDevelopment()
            || configuration.GetValue<bool>("Features:AllowEmailVerificationSimulation");

        if (!enabled)
            return ApiResponse
                .Failure(
                    "Simulação de confirmação indisponível neste ambiente.",
                    "EMAIL_VERIFICATION_SIMULATION_DISABLED",
                    StatusCodes.Status403Forbidden,
                    Request.Path
                )
                .AsResult(StatusCodes.Status403Forbidden);

        var result = await simulateVerification.HandleAsync();
        return result.IsSuccess
            ? Ok(
                ApiResponse.Success(
                    HttpContext,
                    result.Value,
                    "E-mail confirmado por simulação.",
                    "EMAIL_VERIFICATION_SIMULATED"
                )
            )
            : result.Error!.ToActionResult(HttpContext);
    }
}
