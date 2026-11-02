using System.Net;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class GmailTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_RefreshesOnceAndReusesCachedToken()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore { Token = "refresh-token" };
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await GmailTestHelpers.ReadRequestBodyAsync(request, cancellationToken);
            return GmailTestHelpers.Json(HttpStatusCode.OK, """{"access_token":"access-1","expires_in":3600}""");
        });
        var factory = new StubHttpClientFactory(handler);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var provider = new GmailTokenProvider(factory, store, options, time, NullLogger<GmailTokenProvider>.Instance);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("access-1", first);
        Assert.Equal("access-1", second);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(GmailOptions.HttpClientName, factory.LastClientName);
        Assert.NotNull(requestBody);
        Assert.Contains("client_id=test-client-id", requestBody);
        Assert.Contains("client_secret=test-client-secret", requestBody);
        Assert.Contains("refresh_token=refresh-token", requestBody);
        Assert.Contains("grant_type=refresh_token", requestBody);
    }

    [Fact]
    public async Task GetAccessTokenAsync_AfterCacheMargin_RefreshesAgain()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore { Token = "refresh-token" };
        var counter = 0;

        var handler = new StubHttpMessageHandler((_, _) =>
        {
            counter++;
            return Task.FromResult(GmailTestHelpers.Json(
                HttpStatusCode.OK,
                $$"""{"access_token":"access-{{counter}}","expires_in":3600}"""));
        });
        var factory = new StubHttpClientFactory(handler);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var provider = new GmailTokenProvider(factory, store, options, time, NullLogger<GmailTokenProvider>.Instance);

        Assert.Equal("access-1", await provider.GetAccessTokenAsync());
        time.Advance(TimeSpan.FromMinutes(56));
        Assert.Equal("access-2", await provider.GetAccessTokenAsync());
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_WithoutRefreshToken_ThrowsAuthorizationRequired()
    {
        var options = GmailTestHelpers.CreateOptions();
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(HttpStatusCode.OK, """{"access_token":"unused","expires_in":3600}""")));
        var provider = new GmailTokenProvider(
            new StubHttpClientFactory(handler),
            new FakeRefreshTokenStore(),
            options,
            TimeProvider.System,
            NullLogger<GmailTokenProvider>.Instance);

        await Assert.ThrowsAsync<GmailAuthorizationRequiredException>(() => provider.GetAccessTokenAsync());
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_RotatedRefreshToken_IsSaved()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore { Token = "old-refresh-token" };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(
                HttpStatusCode.OK,
                """{"access_token":"access-1","expires_in":3600,"refresh_token":"rotated-refresh-token"}""")));
        var provider = new GmailTokenProvider(
            new StubHttpClientFactory(handler),
            store,
            options,
            TimeProvider.System,
            NullLogger<GmailTokenProvider>.Instance);

        await provider.GetAccessTokenAsync();

        Assert.Equal("rotated-refresh-token", store.Token);
    }

    [Fact]
    public async Task GetAccessTokenAsync_InvalidGrant_DeletesStoredTokenAndThrows()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore { Token = "revoked-token" };
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")));
        var provider = new GmailTokenProvider(
            new StubHttpClientFactory(handler),
            store,
            options,
            TimeProvider.System,
            NullLogger<GmailTokenProvider>.Instance);

        await Assert.ThrowsAsync<GmailAuthorizationRequiredException>(() => provider.GetAccessTokenAsync());

        Assert.Equal(1, store.DeleteCount);
        Assert.Null(store.Token);
    }
}
