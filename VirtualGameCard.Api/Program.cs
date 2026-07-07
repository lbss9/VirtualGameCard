using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VirtualGameCard.Api.Auth;
using VirtualGameCard.Api.Common;
using VirtualGameCard.Application.Auth;
using VirtualGameCard.Application.Auth.Commands;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Application.Notifications;
using VirtualGameCard.Application.Profile;
using VirtualGameCard.Application.Purchases.Commands;
using VirtualGameCard.Application.Purchases.Queries;
using VirtualGameCard.Application.Support;
using VirtualGameCard.Infrastructure;
using VirtualGameCard.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddInfrastructure(builder.Configuration);

// Tratamento global de exceções → contrato ApiError
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Handlers CQRS
builder.Services.AddScoped<RegisterCommandHandler>();
builder.Services.AddScoped<LoginCommandHandler>();
builder.Services.AddScoped<ForgotPasswordCommandHandler>();
builder.Services.AddScoped<ResetPasswordCommandHandler>();
builder.Services.AddScoped<AuthSessionIssuer>();
builder.Services.AddScoped<RefreshSessionCommandHandler>();
builder.Services.AddScoped<LogoutCommandHandler>();
builder.Services.AddScoped<GetProfileQueryHandler>();
builder.Services.AddScoped<ChangePasswordCommandHandler>();
builder.Services.AddScoped<SendEmailVerificationCommandHandler>();
builder.Services.AddScoped<VerifyEmailCommandHandler>();
builder.Services.AddScoped<CreateSupportTicketCommandHandler>();
builder.Services.AddScoped<GetNotificationsQueryHandler>();
builder.Services.AddScoped<MarkNotificationReadCommandHandler>();
builder.Services.AddScoped<MarkAllNotificationsReadCommandHandler>();
builder.Services.AddScoped<PurchaseCardCommandHandler>();
builder.Services.AddScoped<GetPurchasesQueryHandler>();
builder.Services.AddScoped<GetPurchaseByIdQueryHandler>();
builder.Services.AddScoped<ProcessPaymentWebhookCommandHandler>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException(
        "Jwt:Secret deve ser configurado externamente e possuir ao menos 32 bytes."
    );
var paymentWebhookSecret = builder.Configuration["PaymentWebhook:Secret"];
if (
    !builder.Environment.IsDevelopment()
    && (
        string.IsNullOrWhiteSpace(paymentWebhookSecret)
        || Encoding.UTF8.GetByteCount(paymentWebhookSecret) < 32
    )
)
    throw new InvalidOperationException(
        "PaymentWebhook:Secret deve ser configurado externamente e possuir ao menos 32 bytes."
    );
builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var rawSessionId = context.Principal?.FindFirst("sid")?.Value;
                if (
                    !Guid.TryParse(rawSessionId, out var sessionId)
                    || !await context
                        .HttpContext.RequestServices.GetRequiredService<VirtualGameCard.Domain.Interfaces.IRefreshSessionRepository>()
                        .IsActiveAsync(sessionId, context.HttpContext.RequestAborted)
                )
                    context.Fail("Sessão revogada ou inválida.");
            },
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddResponseCompression();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Failure(
                "Muitas tentativas. Aguarde um pouco e tente novamente.",
                "RATE_LIMITED",
                StatusCodes.Status429TooManyRequests,
                context.HttpContext.Request.Path
            ),
            cancellationToken
        );
    };
    AddPolicy(
        "auth",
        builder.Configuration.GetValue<int?>("RateLimiting:AuthPermitLimit") ?? 10,
        TimeSpan.FromMinutes(1)
    );
    AddPolicy(
        "sensitive",
        builder.Configuration.GetValue<int?>("RateLimiting:SensitivePermitLimit") ?? 5,
        TimeSpan.FromMinutes(10)
    );
    AddPolicy(
        "purchase",
        builder.Configuration.GetValue<int?>("RateLimiting:PurchasePermitLimit") ?? 10,
        TimeSpan.FromMinutes(1)
    );
    AddPolicy(
        "support",
        builder.Configuration.GetValue<int?>("RateLimiting:SupportPermitLimit") ?? 5,
        TimeSpan.FromMinutes(10)
    );
    AddPolicy(
        "webhook",
        builder.Configuration.GetValue<int?>("RateLimiting:WebhookPermitLimit") ?? 60,
        TimeSpan.FromMinutes(1)
    );

    void AddPolicy(string name, int permits, TimeSpan window) =>
        options.AddPolicy(
            name,
            context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.User.Identity?.IsAuthenticated == true
                        ? context.User.FindFirst("sub")?.Value ?? "authenticated"
                        : context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permits,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }
                )
        );
});
builder
    .Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            ApiResponse
                .Failure(
                    "Os dados enviados são inválidos.",
                    "VALIDATION_ERROR",
                    StatusCodes.Status400BadRequest,
                    context.HttpContext.Request.Path
                )
                .AsResult(StatusCodes.Status400BadRequest);
    });

var allowedOrigins =
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ??
    [
        "http://localhost:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5173",
        "http://127.0.0.1:5174",
    ];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()
    )
);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(
        (doc, ctx, ct) =>
        {
            foreach (var server in doc.Servers ?? [])
                server.Url = server.Url?.TrimEnd('/') ?? server.Url;
            return Task.CompletedTask;
        }
    );
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsRelational())
        await dbContext.Database.MigrateAsync();
    else
        await dbContext.Database.EnsureCreatedAsync();
}

// Exceções não tratadas → ApiError 500 (primeiro no pipeline)
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseResponseCompression();

// Status do framework sem corpo (401/404) → ApiError
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.HasStarted || !string.IsNullOrEmpty(response.ContentType))
        return;

    var error = ApiResponse.FromStatus(response.StatusCode, context.HttpContext.Request.Path);
    await response.WriteAsJsonAsync(error);
});

app.MapOpenApi();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program { }
