using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtualGameCard.Application.Common;
using VirtualGameCard.Infrastructure.Data;
using Xunit;

namespace VirtualGameCard.Tests;

public sealed class ApiRoutesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory factory = factory;
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task OpenApi_document_contains_every_public_route()
    {
        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await Body(response);
        var paths = document.GetProperty("paths");
        string[] expected =
        [
            "/api/auth/register",
            "/api/auth/login",
            "/api/auth/forgot-password",
            "/api/auth/reset-password",
            "/api/auth/verify-email",
            "/api/auth/refresh",
            "/api/auth/logout",
            "/api/me",
            "/api/me/password",
            "/api/me/email-verification",
            "/api/cards/purchase",
            "/api/purchases",
            "/api/purchases/{id}",
            "/api/purchases/{id}/simulate-approval",
            "/api/support/tickets",
            "/api/notifications",
            "/api/notifications/{id}/read",
            "/api/notifications/read-all",
            "/api/payments/webhooks",
        ];
        foreach (var path in expected)
            Assert.True(paths.TryGetProperty(path, out _), $"OpenAPI sem a rota {path}");
        var schemas = document.GetProperty("components").GetProperty("schemas");
        var schemaNames = schemas.EnumerateObject().Select(x => x.Name).ToList();
        Assert.Contains(schemaNames, x => x.Contains("PurchaseData", StringComparison.Ordinal));
        Assert.Contains(schemaNames, x => x.Contains("AuthResponse", StringComparison.Ordinal));
        Assert.Contains(
            schemaNames,
            x => x.Contains("NotificationsResult", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Register_and_login_return_standard_envelope()
    {
        var account = NewAccount();
        var register = await client.PostAsJsonAsync("/api/auth/register", account);

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var registered = await Body(register);
        AssertEnvelope(registered, "REGISTER_SUCCESS", 201);
        Assert.False(
            string.IsNullOrWhiteSpace(
                registered.GetProperty("data").GetProperty("token").GetString()
            )
        );

        var login = await client.PostAsJsonAsync("/api/auth/login", account);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AssertEnvelope(await Body(login), "LOGIN_SUCCESS", 200);
    }

    [Fact]
    public async Task Authentication_errors_return_standard_envelope()
    {
        var account = NewAccount();
        await client.PostAsJsonAsync("/api/auth/register", account);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register", account);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        AssertEnvelope(await Body(duplicate), "EMAIL_ALREADY_EXISTS", 409);

        var invalid = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { account.Email, Password = "WrongPassword1!" }
        );
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        AssertEnvelope(await Body(invalid), "INVALID_CREDENTIALS", 401);

        var malformed = await client.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        AssertEnvelope(await Body(malformed), "VALIDATION_ERROR", 400);
    }

    [Fact]
    public async Task Forgot_and_reset_password_complete_the_flow()
    {
        var account = NewAccount();
        await client.PostAsJsonAsync("/api/auth/register", account);

        var forgot = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { account.Email }
        );
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        var forgotBody = await Body(forgot);
        AssertEnvelope(forgotBody, "PASSWORD_RESET_REQUESTED", 200);
        var token = forgotBody.GetProperty("data").GetProperty("resetToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { Token = token, NewPassword = "NewPassword2!" }
        );
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        AssertEnvelope(await Body(reset), "PASSWORD_RESET_SUCCESS", 200);

        var reused = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { Token = token, NewPassword = "AnotherPassword3!" }
        );
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        AssertEnvelope(await Body(reused), "RESET_TOKEN_INVALID", 400);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { account.Email, Password = "NewPassword2!" }
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Expired_password_reset_token_is_rejected()
    {
        var account = NewAccount();
        await client.PostAsJsonAsync("/api/auth/register", account);
        var forgot = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new { account.Email }
        );
        var token = (await Body(forgot)).GetProperty("data").GetProperty("resetToken").GetString()!;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.PasswordResetTokens.SingleAsync(x =>
                x.TokenHash == SecureToken.Hash(token)
            );
            stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        var reset = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { Token = token, NewPassword = "NewPassword2!" }
        );
        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        AssertEnvelope(await Body(reset), "RESET_TOKEN_EXPIRED", 400);
    }

    [Fact]
    public async Task Refresh_rotates_session_and_logout_revokes_it()
    {
        var session = await Register();

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await Body(refresh);
        AssertEnvelope(refreshed, "SESSION_REFRESHED", 200);

        Authenticate(refreshed.GetProperty("data").GetProperty("token").GetString()!);
        var logout = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var revokedAccess = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedAccess.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var afterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
        AssertEnvelope(await Body(afterLogout), "REFRESH_TOKEN_INVALID", 401);
    }

    [Fact]
    public async Task Reusing_rotated_refresh_token_revokes_the_session_family()
    {
        using var manual = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = false,
            }
        );
        var account = NewAccount();
        var register = await manual.PostAsJsonAsync("/api/auth/register", account);
        var oldCookie = register.Headers.GetValues("Set-Cookie").Single().Split(';')[0];

        manual.DefaultRequestHeaders.Add("Cookie", oldCookie);
        var firstRefresh = await manual.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var reused = await manual.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        AssertEnvelope(await Body(reused), "REFRESH_TOKEN_REUSED", 401);
    }

    [Fact]
    public async Task Logout_revokes_access_session_even_when_refresh_cookie_is_missing()
    {
        using var noCookies = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = false,
            }
        );
        var register = await noCookies.PostAsJsonAsync("/api/auth/register", NewAccount());
        var token = (await Body(register)).GetProperty("data").GetProperty("token").GetString()!;
        noCookies.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await noCookies.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await noCookies.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Profile_requires_authentication_and_returns_current_user()
    {
        var unauthorized = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        AssertEnvelope(await Body(unauthorized), "UNAUTHORIZED", 401);

        var session = await Register();
        Authenticate(session.Token);
        var response = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Body(response);
        AssertEnvelope(body, "PROFILE_RETRIEVED", 200);
        Assert.Equal(session.Email, body.GetProperty("data").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Email_verification_and_password_change_complete_the_flow()
    {
        var session = await Register();
        Authenticate(session.Token);

        var sent = await client.PostAsJsonAsync("/api/me/email-verification", new { });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var token = (await Body(sent))
            .GetProperty("data")
            .GetProperty("verificationToken")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = null;
        var verified = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { Token = token }
        );
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        AssertEnvelope(await Body(verified), "EMAIL_VERIFIED", 200);

        var reusedVerification = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { Token = token }
        );
        Assert.Equal(HttpStatusCode.BadRequest, reusedVerification.StatusCode);
        AssertEnvelope(await Body(reusedVerification), "VERIFICATION_TOKEN_INVALID", 400);

        Authenticate(session.Token);
        var changed = await client.PostAsJsonAsync(
            "/api/me/password",
            new { CurrentPassword = session.Password, NewPassword = "ChangedPassword3!" }
        );
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        AssertEnvelope(await Body(changed), "PASSWORD_CHANGED", 200);
    }

    [Fact]
    public async Task Verified_user_can_purchase_list_and_open_card_details()
    {
        var session = await RegisterAndVerify();
        Authenticate(session.Token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var purchase = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = 50,
                Platform = "steam",
                PaymentMethod = "pix",
            }
        );
        Assert.Equal(HttpStatusCode.Created, purchase.StatusCode);
        var purchased = await Body(purchase);
        AssertEnvelope(purchased, "PURCHASE_CREATED", 201);
        var id = purchased.GetProperty("data").GetProperty("id").GetGuid();
        var paymentReference = purchased
            .GetProperty("data")
            .GetProperty("paymentReference")
            .GetString()!;
        Assert.Equal("pending", purchased.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(
            JsonValueKind.Null,
            purchased.GetProperty("data").GetProperty("code").ValueKind
        );

        var list = await client.GetAsync("/api/purchases?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listed = await Body(list);
        AssertEnvelope(listed, "PURCHASES_RETRIEVED", 200);
        Assert.Equal(50, listed.GetProperty("data").GetProperty("pageSize").GetInt32());
        Assert.Contains(
            listed.GetProperty("data").GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == id
        );

        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = $"{eventId}.{paymentReference}.approved";
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes("integration-test-webhook-secret"),
                Encoding.UTF8.GetBytes(payload)
            )
        );
        using var invalidWebhook = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks")
        {
            Content = JsonContent.Create(
                new
                {
                    EventId = $"bad_{Guid.NewGuid():N}",
                    PaymentReference = paymentReference,
                    Status = "approved",
                }
            ),
        };
        invalidWebhook.Headers.Add("X-Payment-Signature", "00");
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.SendAsync(invalidWebhook)).StatusCode
        );

        using var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks")
        {
            Content = JsonContent.Create(
                new
                {
                    EventId = eventId,
                    PaymentReference = paymentReference,
                    Status = "approved",
                }
            ),
        };
        webhookRequest.Headers.Add("X-Payment-Signature", signature);
        var webhook = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);

        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks")
        {
            Content = JsonContent.Create(
                new
                {
                    EventId = eventId,
                    PaymentReference = paymentReference,
                    Status = "approved",
                }
            ),
        };
        replayRequest.Headers.Add("X-Payment-Signature", signature);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(replayRequest)).StatusCode);

        var detail = await client.GetAsync($"/api/purchases/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await Body(detail);
        AssertEnvelope(detailBody, "PURCHASE_RETRIEVED", 200);
        Assert.False(
            string.IsNullOrWhiteSpace(
                detailBody.GetProperty("data").GetProperty("code").GetString()
            )
        );
    }

    [Fact]
    public async Task Payment_simulation_approves_pending_purchase_for_owner()
    {
        var session = await RegisterAndVerify();
        Authenticate(session.Token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var purchase = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = 50,
                Platform = "steam",
                PaymentMethod = "pix",
            }
        );
        Assert.Equal(HttpStatusCode.Created, purchase.StatusCode);
        var created = await Body(purchase);
        var purchaseId = created.GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal("pending", created.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("data").GetProperty("code").ValueKind);

        var approved = await client.PostAsJsonAsync(
            $"/api/purchases/{purchaseId}/simulate-approval",
            new { }
        );
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var approvedBody = await Body(approved);
        AssertEnvelope(approvedBody, "PAYMENT_SIMULATED", 200);
        Assert.Equal("approved", approvedBody.GetProperty("data").GetProperty("status").GetString());
        Assert.False(
            string.IsNullOrWhiteSpace(
                approvedBody.GetProperty("data").GetProperty("code").GetString()
            )
        );

        var other = await RegisterAndVerify();
        Authenticate(other.Token);
        var hidden = await client.PostAsJsonAsync(
            $"/api/purchases/{purchaseId}/simulate-approval",
            new { }
        );
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        AssertEnvelope(await Body(hidden), "PURCHASE_NOT_FOUND", 404);
    }

    [Fact]
    public async Task Purchase_validates_account_and_card_input()
    {
        var session = await Register();
        Authenticate(session.Token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var unverified = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = 50,
                Platform = "steam",
                PaymentMethod = "pix",
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, unverified.StatusCode);
        AssertEnvelope(await Body(unverified), "ACCOUNT_NOT_VERIFIED", 400);

        session = await RegisterAndVerify();
        Authenticate(session.Token);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var invalid = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = 51,
                Platform = "unknown",
                PaymentMethod = "cash",
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        AssertEnvelope(await Body(invalid), "VALIDATION_ERROR", 400);
    }

    [Theory]
    [InlineData(0, "steam", "pix", true, "VALIDATION_ERROR")]
    [InlineData(255, "steam", "pix", true, "VALIDATION_ERROR")]
    [InlineData(6, "steam", "pix", true, "VALIDATION_ERROR")]
    [InlineData(25, "invalid", "pix", true, "VALIDATION_ERROR")]
    [InlineData(25, "steam", "cash", true, "VALIDATION_ERROR")]
    [InlineData(25, "steam", "pix", false, "IDEMPOTENCY_KEY_INVALID")]
    public async Task Purchase_rejects_each_invalid_field(
        int amount,
        string platform,
        string method,
        bool sendIdempotencyKey,
        string expectedCode
    )
    {
        var session = await RegisterAndVerify();
        Authenticate(session.Token);
        if (sendIdempotencyKey)
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var response = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = amount,
                Platform = platform,
                PaymentMethod = method,
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertEnvelope(await Body(response), expectedCode, 400);
    }

    [Fact]
    public async Task Purchase_idempotency_returns_same_order_and_rejects_changed_payload()
    {
        var session = await RegisterAndVerify();
        Authenticate(session.Token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var body = new
        {
            Amount = 25,
            Platform = "steam",
            PaymentMethod = "pix",
        };

        var first = await client.PostAsJsonAsync("/api/cards/purchase", body);
        var second = await client.PostAsJsonAsync("/api/cards/purchase", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(
            (await Body(first)).GetProperty("data").GetProperty("id").GetGuid(),
            (await Body(second)).GetProperty("data").GetProperty("id").GetGuid()
        );

        var conflict = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = 30,
                Platform = "steam",
                PaymentMethod = "pix",
            }
        );
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        AssertEnvelope(await Body(conflict), "IDEMPOTENCY_KEY_REUSED", 409);
    }

    [Fact]
    public async Task Purchases_are_isolated_between_users_and_pagination_is_strict()
    {
        var owner = await RegisterAndVerify();
        Authenticate(owner.Token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var purchase = await client.PostAsJsonAsync(
            "/api/cards/purchase",
            new
            {
                Amount = 20,
                Platform = "xbox",
                PaymentMethod = "card",
            }
        );
        var purchaseId = (await Body(purchase)).GetProperty("data").GetProperty("id").GetGuid();

        var other = await Register();
        Authenticate(other.Token);
        var hidden = await client.GetAsync($"/api/purchases/{purchaseId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        AssertEnvelope(await Body(hidden), "PURCHASE_NOT_FOUND", 404);

        var badPage = await client.GetAsync("/api/purchases?page=0&pageSize=20");
        Assert.Equal(HttpStatusCode.BadRequest, badPage.StatusCode);
        AssertEnvelope(await Body(badPage), "INVALID_PAGE", 400);
        var badSize = await client.GetAsync("/api/purchases?page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.BadRequest, badSize.StatusCode);
        AssertEnvelope(await Body(badSize), "INVALID_PAGE_SIZE", 400);
    }

    [Fact]
    public async Task Support_ticket_creates_notification_that_can_be_read()
    {
        var session = await Register();
        Authenticate(session.Token);
        var ticket = await client.PostAsJsonAsync(
            "/api/support/tickets",
            new
            {
                Subject = "Problema com o card",
                Category = "code",
                Message = "Meu código ainda não foi reconhecido.",
            }
        );
        Assert.Equal(HttpStatusCode.Created, ticket.StatusCode);
        AssertEnvelope(await Body(ticket), "SUPPORT_TICKET_CREATED", 201);

        var notifications = await client.GetAsync("/api/notifications");
        var body = await Body(notifications);
        AssertEnvelope(body, "NOTIFICATIONS_RETRIEVED", 200);
        Assert.Equal(2, body.GetProperty("data").GetProperty("unreadCount").GetInt32());
        var notificationId = body.GetProperty("data")
            .GetProperty("items")[0]
            .GetProperty("id")
            .GetGuid();

        var read = await client.PatchAsync($"/api/notifications/{notificationId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        var readAll = await client.PostAsJsonAsync("/api/notifications/read-all", new { });
        Assert.Equal(HttpStatusCode.NoContent, readAll.StatusCode);
    }

    [Fact]
    public async Task Support_ticket_rejects_invalid_subject_category_and_message()
    {
        var session = await Register();
        Authenticate(session.Token);
        object[] invalidRequests =
        [
            new
            {
                Subject = "abc",
                Category = "code",
                Message = "Mensagem válida para teste.",
            },
            new
            {
                Subject = "Assunto válido",
                Category = "unknown",
                Message = "Mensagem válida para teste.",
            },
            new
            {
                Subject = "Assunto válido",
                Category = "code",
                Message = "curta",
            },
        ];
        foreach (var request in invalidRequests)
        {
            var response = await client.PostAsJsonAsync("/api/support/tickets", request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            AssertEnvelope(await Body(response), "VALIDATION_ERROR", 400);
        }
    }

    private async Task<Session> RegisterAndVerify()
    {
        var session = await Register();
        Authenticate(session.Token);
        var sent = await client.PostAsJsonAsync("/api/me/email-verification", new { });
        var token = (await Body(sent))
            .GetProperty("data")
            .GetProperty("verificationToken")
            .GetString();
        client.DefaultRequestHeaders.Authorization = null;
        await client.PostAsJsonAsync("/api/auth/verify-email", new { Token = token });
        return session;
    }

    private async Task<Session> Register()
    {
        var account = NewAccount();
        var response = await client.PostAsJsonAsync("/api/auth/register", account);
        var body = await Body(response);
        return new Session(
            account.Email,
            account.Password,
            body.GetProperty("data").GetProperty("token").GetString()!
        );
    }

    private void Authenticate(string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Account NewAccount() =>
        new($"user-{Guid.NewGuid():N}@example.com", "StrongPassword1!");

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());

    private static void AssertEnvelope(JsonElement body, string code, int statusCode)
    {
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(statusCode, body.GetProperty("statusCode").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.StartsWith("/api/", body.GetProperty("path").GetString());
        Assert.True(body.TryGetProperty("data", out _));
    }

    private sealed record Account(string Email, string Password);

    private sealed record Session(string Email, string Password, string Token);
}
