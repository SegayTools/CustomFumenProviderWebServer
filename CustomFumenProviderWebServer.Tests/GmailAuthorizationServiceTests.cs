using System.Net;
using System.Text;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class GmailAuthorizationServiceTests
{
    [Fact]
    public void CreateAuthorizationUri_ContainsOfflineConsentAndExpectedScopes()
    {
        var options = GmailTestHelpers.CreateOptions();
        var service = CreateService(options, new FakeRefreshTokenStore(), new FakeGmailAccountStore(), out _);

        var uri = service.CreateAuthorizationUri("state-123");
        var query = QueryHelpers.ParseQuery(uri.Query);
        var scope = query["scope"].ToString();

        Assert.Equal("https", uri.Scheme);
        Assert.Equal("accounts.google.com", uri.Host);
        Assert.Equal(options.ClientId, query["client_id"].ToString());
        Assert.Equal(options.RedirectUri, query["redirect_uri"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Contains("openid", scope);
        Assert.Contains("email", scope);
        Assert.Contains(GmailOptions.SendScope, scope);
        Assert.Equal("offline", query["access_type"].ToString());
        Assert.Equal("consent", query["prompt"].ToString());
        Assert.Equal("state-123", query["state"].ToString());
    }

    [Fact]
    public async Task ExchangeCodeAsync_StoresReturnedRefreshToken()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore();
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await GmailTestHelpers.ReadRequestBodyAsync(request, cancellationToken);
            return GmailTestHelpers.Json(HttpStatusCode.OK, """{"refresh_token":"refresh-1"}""");
        });
        var service = CreateService(options, store, new FakeGmailAccountStore(), out var factory, handler);

        await service.ExchangeCodeAsync("code-1");

        Assert.Equal("refresh-1", store.Token);
        Assert.Equal(GmailOptions.HttpClientName, factory.LastClientName);
        Assert.NotNull(requestBody);
        Assert.Contains("code=code-1", requestBody);
        Assert.Contains("grant_type=authorization_code", requestBody);
        Assert.Contains("client_id=test-client-id", requestBody);
        Assert.Contains("client_secret=test-client-secret", requestBody);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithIdToken_SavesAuthorizedEmail()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore();
        var accountStore = new FakeGmailAccountStore();
        var idToken = CreateIdToken(options.ClientId, "authorized@example.com");
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(
                HttpStatusCode.OK,
                $$"""{"refresh_token":"refresh-1","id_token":"{{idToken}}"}""")));
        var service = CreateService(options, store, accountStore, out _, handler);

        await service.ExchangeCodeAsync("code-1");

        Assert.Equal("authorized@example.com", accountStore.SenderAddress);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithIdTokenForAnotherClient_DoesNotSaveEmail()
    {
        var options = GmailTestHelpers.CreateOptions();
        var accountStore = new FakeGmailAccountStore();
        var idToken = CreateIdToken("another-client-id", "other@example.com");
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(
                HttpStatusCode.OK,
                $$"""{"refresh_token":"refresh-1","id_token":"{{idToken}}"}""")));
        var service = CreateService(options, new FakeRefreshTokenStore(), accountStore, out _, handler);

        await service.ExchangeCodeAsync("code-1");

        Assert.Null(accountStore.SenderAddress);
    }

    [Fact]
    public async Task ExchangeCodeAsync_WithoutRefreshToken_Throws()
    {
        var options = GmailTestHelpers.CreateOptions();
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(HttpStatusCode.OK, """{"access_token":"access-only"}""")));
        var service = CreateService(options, new FakeRefreshTokenStore(), new FakeGmailAccountStore(), out _, handler);

        await Assert.ThrowsAsync<GmailAuthorizationException>(() => service.ExchangeCodeAsync("code-1"));
    }

    private static string CreateIdToken(string audience, string email)
    {
        var payload = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(
            $$"""{"aud":"{{audience}}","iss":"https://accounts.google.com","email":"{{email}}"}"""));
        return $"e30.{payload}.signature";
    }

    private static GmailAuthorizationService CreateService(
        GmailOptions options,
        IGmailRefreshTokenStore store,
        IGmailAccountStore accountStore,
        out StubHttpClientFactory factory)
    {
        return CreateService(
            options,
            store,
            accountStore,
            out factory,
            new StubHttpMessageHandler((_, _) =>
                Task.FromResult(GmailTestHelpers.Json(HttpStatusCode.OK, """{"refresh_token":"refresh-1"}"""))));
    }

    private static GmailAuthorizationService CreateService(
        GmailOptions options,
        IGmailRefreshTokenStore store,
        IGmailAccountStore accountStore,
        out StubHttpClientFactory factory,
        HttpMessageHandler handler)
    {
        factory = new StubHttpClientFactory(handler);
        return new GmailAuthorizationService(
            factory,
            store,
            accountStore,
            options,
            NullLogger<GmailAuthorizationService>.Instance);
    }
}
