using System.Net;
using System.Text.Json;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;

namespace CustomFumenProviderWebServer.Tests;

public class GmailEmailSenderTests
{
    [Fact]
    public async Task SendAsync_PostsBase64UrlEncodedMimeMessage()
    {
        var options = GmailTestHelpers.CreateOptions();
        string? authorization = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            authorization = request.Headers.Authorization?.ToString();
            requestBody = await GmailTestHelpers.ReadRequestBodyAsync(request, cancellationToken);
            return GmailTestHelpers.Json(HttpStatusCode.OK, """{"id":"message-1"}""");
        });
        var sender = new GmailEmailSender(
            new StubHttpClientFactory(handler),
            new FakeAccessTokenProvider("access-1"),
            new FakeGmailAccountStore(),
            options,
            TimeProvider.System);

        var messageId = await sender.SendAsync(new GmailMessage
        {
            To = new[] { "receiver@example.com" },
            Subject = "hello",
            TextBody = "world",
        });

        Assert.Equal("message-1", messageId);
        Assert.Equal("Bearer access-1", authorization);
        Assert.NotNull(requestBody);

        using var document = JsonDocument.Parse(requestBody);
        var raw = document.RootElement.GetProperty("raw").GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));

        using var mime = MimeMessage.Load(new MemoryStream(WebEncoders.Base64UrlDecode(raw!)));
        Assert.Equal("sender@example.com", mime.From.Mailboxes.Single().Address);
        Assert.Equal("receiver@example.com", mime.To.Mailboxes.Single().Address);
        Assert.Equal("hello", mime.Subject);
        Assert.Equal("world", mime.TextBody!.Trim());
    }

    [Fact]
    public async Task SendAsync_OnUnauthorized_RefreshesTokenAndRetriesOnce()
    {
        var options = GmailTestHelpers.CreateOptions();
        var attempt = 0;

        var handler = new StubHttpMessageHandler((_, _) =>
        {
            attempt++;
            return Task.FromResult(attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : GmailTestHelpers.Json(HttpStatusCode.OK, """{"id":"message-2"}"""));
        });
        var tokenProvider = new FakeAccessTokenProvider("expired-token", "fresh-token");
        var sender = new GmailEmailSender(
            new StubHttpClientFactory(handler),
            tokenProvider,
            new FakeGmailAccountStore(),
            options,
            TimeProvider.System);

        var messageId = await sender.SendAsync(new GmailMessage
        {
            To = new[] { "receiver@example.com" },
            Subject = "retry",
        });

        Assert.Equal("message-2", messageId);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(1, tokenProvider.InvalidateCount);
    }

    [Fact]
    public async Task SendAsync_GmailError_ThrowsWithGoogleMessage()
    {
        var options = GmailTestHelpers.CreateOptions();
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(
                HttpStatusCode.BadRequest,
                """{"error":{"code":400,"message":"Invalid To header","status":"INVALID_ARGUMENT"}}""")));
        var sender = new GmailEmailSender(
            new StubHttpClientFactory(handler),
            new FakeAccessTokenProvider("access-1"),
            new FakeGmailAccountStore(),
            options,
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<GmailSendException>(() => sender.SendAsync(new GmailMessage
        {
            To = new[] { "receiver@example.com" },
            Subject = "invalid",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("Invalid To header", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithoutConfiguredSender_UsesStoredAuthorizedAddress()
    {
        var options = GmailTestHelpers.CreateOptions(senderAddress: "");
        var accountStore = new FakeGmailAccountStore { SenderAddress = "authorized@example.com" };
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await GmailTestHelpers.ReadRequestBodyAsync(request, cancellationToken);
            return GmailTestHelpers.Json(HttpStatusCode.OK, """{"id":"message-3"}""");
        });
        var sender = new GmailEmailSender(
            new StubHttpClientFactory(handler),
            new FakeAccessTokenProvider("access-1"),
            accountStore,
            options,
            TimeProvider.System);

        await sender.SendAsync(new GmailMessage
        {
            To = new[] { "receiver@example.com" },
            Subject = "auto sender",
        });

        Assert.NotNull(requestBody);
        using var document = JsonDocument.Parse(requestBody);
        var raw = document.RootElement.GetProperty("raw").GetString();
        using var mime = MimeMessage.Load(new MemoryStream(WebEncoders.Base64UrlDecode(raw!)));
        Assert.Equal("authorized@example.com", mime.From.Mailboxes.Single().Address);
    }

    [Fact]
    public async Task SendAsync_WithoutAnySender_ThrowsAuthorizationRequired()
    {
        var options = GmailTestHelpers.CreateOptions(senderAddress: "");
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(HttpStatusCode.OK, """{"id":"unused"}""")));
        var sender = new GmailEmailSender(
            new StubHttpClientFactory(handler),
            new FakeAccessTokenProvider("access-1"),
            new FakeGmailAccountStore(),
            options,
            TimeProvider.System);

        await Assert.ThrowsAsync<GmailAuthorizationRequiredException>(() => sender.SendAsync(new GmailMessage
        {
            To = new[] { "receiver@example.com" },
        }));
        Assert.Equal(0, handler.RequestCount);
    }
}
