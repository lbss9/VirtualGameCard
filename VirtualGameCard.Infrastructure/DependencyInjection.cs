using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtualGameCard.Application.Interfaces;
using VirtualGameCard.Domain.Interfaces;
using VirtualGameCard.Infrastructure.Data;
using VirtualGameCard.Infrastructure.Messaging;
using VirtualGameCard.Infrastructure.Repositories;
using VirtualGameCard.Infrastructure.Services;

namespace VirtualGameCard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString("Default")
            ?? NormalizeDatabaseUrl(configuration["DATABASE_URL"])
            ?? throw new InvalidOperationException(
                "Configure ConnectionStrings:Default ou DATABASE_URL para conectar ao PostgreSQL."
            );

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IGiftCardPurchaseRepository, GiftCardPurchaseRepository>();
        services.AddScoped<IEmailVerificationTokenRepository, EmailVerificationTokenRepository>();
        services.AddScoped<ISupportTicketRepository, SupportTicketRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();
        services.AddScoped<IPaymentWebhookEventRepository, PaymentWebhookEventRepository>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPaymentWebhookVerifier, PaymentWebhookVerifier>();
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddScoped<IPaymentMessagePublisher>(
            provider =>
                configuration.GetValue<bool>("RabbitMq:Enabled")
                    ? provider.GetRequiredService<RabbitMqPaymentMessagePublisher>()
                    : provider.GetRequiredService<NoOpPaymentMessagePublisher>()
        );
        services.AddScoped<RabbitMqPaymentMessagePublisher>();
        services.AddScoped<NoOpPaymentMessagePublisher>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }

    private static string? NormalizeDatabaseUrl(string? databaseUrl)
    {
        if (string.IsNullOrWhiteSpace(databaseUrl))
            return null;

        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri))
            return databaseUrl;

        if (uri.Scheme is not ("postgres" or "postgresql"))
            return databaseUrl;

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? "");
        var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? "");
        var database = uri.AbsolutePath.TrimStart('/');

        return string.Join(
            ';',
            $"Host={uri.Host}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={database}",
            $"Username={username}",
            $"Password={password}",
            "SSL Mode=Require",
            "Trust Server Certificate=true"
        );
    }
}
