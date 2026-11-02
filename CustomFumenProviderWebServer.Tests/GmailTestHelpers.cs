#nullable enable

using System.Net;
using System.Text;
using CustomFumenProviderWebServer.Services.Gmail;

namespace CustomFumenProviderWebServer.Tests;

internal static class GmailTestHelpers
{
    public static GmailOptions CreateOptions(string? dataDirectory = null, string senderAddress = "sender@example.com")
    {
        return new GmailOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "https://example.com/oauth/google/callback",
            SenderAddress = senderAddress,
            DataDirectory = dataDirectory ?? CreateTempDirectory(),
        };
    }

    public static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "fumen-gmail-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public static async Task<string?> ReadRequestBodyAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
    }
}

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        this.responder = responder;
    }

    public int RequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return await responder(request, cancellationToken);
    }
}

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler handler;

    public StubHttpClientFactory(HttpMessageHandler handler)
    {
        this.handler = handler;
    }

    public string? LastClientName { get; private set; }

    public HttpClient CreateClient(string name)
    {
        LastClientName = name;
        return new HttpClient(handler, disposeHandler: false);
    }
}

internal sealed class FakeAccessTokenProvider : IGmailAccessTokenProvider
{
    private readonly Queue<string> tokens;

    public FakeAccessTokenProvider(params string[] tokens)
    {
        this.tokens = new Queue<string>(tokens);
    }

    public int InvalidateCount { get; private set; }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(tokens.Dequeue());
    }

    public Task<bool> HasRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public void Invalidate()
    {
        InvalidateCount++;
    }
}

internal sealed class FakeGmailAccountStore : IGmailAccountStore
{
    public string? SenderAddress { get; set; }

    public Task<string?> ReadSenderAddressAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SenderAddress);
    }

    public Task WriteSenderAddressAsync(string senderAddress, CancellationToken cancellationToken = default)
    {
        SenderAddress = senderAddress;
        return Task.CompletedTask;
    }
}

internal sealed class FakeRefreshTokenStore : IGmailRefreshTokenStore
{
    public string? Token { get; set; }

    public int DeleteCount { get; private set; }

    public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Token);
    }

    public Task WriteAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        Token = refreshToken;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        DeleteCount++;
        Token = null;
        return Task.CompletedTask;
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        this.utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }

    public void Advance(TimeSpan duration)
    {
        utcNow = utcNow.Add(duration);
    }
}
