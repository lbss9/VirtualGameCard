using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace VirtualGameCard.Tests;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Authentication_policy_rejects_requests_over_the_limit()
    {
        await using var factory = new LowLimitApiFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/login", Credentials());
        await client.PostAsJsonAsync("/api/auth/login", Credentials());
        var rejected = await client.PostAsJsonAsync("/api/auth/login", Credentials());

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var body = await rejected.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("RATE_LIMITED", body.GetProperty("code").GetString());
    }

    private static object Credentials() =>
        new { Email = $"missing-{Guid.NewGuid():N}@example.com", Password = "StrongPassword1!" };

    private sealed class LowLimitApiFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:AuthPermitLimit", "2");
        }
    }
}
