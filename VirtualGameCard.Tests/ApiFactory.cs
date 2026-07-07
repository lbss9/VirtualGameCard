using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VirtualGameCard.Infrastructure.Data;

namespace VirtualGameCard.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
        builder.UseSetting("RateLimiting:SensitivePermitLimit", "1000");
        builder.UseSetting("RateLimiting:PurchasePermitLimit", "1000");
        builder.UseSetting("RateLimiting:SupportPermitLimit", "1000");
        builder.UseSetting("RateLimiting:WebhookPermitLimit", "1000");
        builder.UseSetting("PaymentWebhook:Secret", "integration-test-webhook-secret");
        builder.UseSetting("Sqs:Enabled", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(databaseName));
        });
    }
}
