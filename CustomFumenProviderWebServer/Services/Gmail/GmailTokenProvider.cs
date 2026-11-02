#nullable enable

using System.Security.Cryptography;
using System.Text.Json;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    public interface IGmailAccessTokenProvider
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

        Task<bool> HasRefreshTokenAsync(CancellationToken cancellationToken = default);

        void Invalidate();
    }

    /// <summary>
    /// 使用 Refresh Token 换取 Gmail API 的 Access Token，并缓存到过期前 5 分钟。
    /// 同一进程内并发请求只会触发一次刷新。
    /// </summary>
    public sealed class GmailTokenProvider : IGmailAccessTokenProvider
    {
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(5);

        private readonly IHttpClientFactory httpClientFactory;
        private readonly IGmailRefreshTokenStore refreshTokenStore;
        private readonly GmailOptions options;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<GmailTokenProvider> logger;
        private readonly SemaphoreSlim refreshGate = new(1, 1);

        private CachedToken? cachedToken;

        public GmailTokenProvider(
            IHttpClientFactory httpClientFactory,
            IGmailRefreshTokenStore refreshTokenStore,
            GmailOptions options,
            TimeProvider timeProvider,
            ILogger<GmailTokenProvider> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.refreshTokenStore = refreshTokenStore;
            this.options = options;
            this.timeProvider = timeProvider;
            this.logger = logger;
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            var now = timeProvider.GetUtcNow();
            var current = Volatile.Read(ref cachedToken);
            if (current is not null && now < current.ExpiresAt - RefreshMargin)
                return current.AccessToken;

            await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                now = timeProvider.GetUtcNow();
                current = Volatile.Read(ref cachedToken);
                if (current is not null && now < current.ExpiresAt - RefreshMargin)
                    return current.AccessToken;

                var refreshToken = await GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    throw new GmailAuthorizationRequiredException(
                        "Gmail is not authorized. Open /oauth/google/start to complete the Google OAuth authorization.");
                }

                var result = await RequestAccessTokenAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                var fresh = new CachedToken(result.AccessToken, now.AddSeconds(result.ExpiresInSeconds));
                Volatile.Write(ref cachedToken, fresh);

                logger.LogInformation("Refreshed Gmail access token, valid until {ExpiresAt}.", fresh.ExpiresAt);
                return fresh.AccessToken;
            }
            finally
            {
                refreshGate.Release();
            }
        }

        public async Task<bool> HasRefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            return !string.IsNullOrWhiteSpace(await GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false));
        }

        public void Invalidate()
        {
            Volatile.Write(ref cachedToken, null);
        }

        private async Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken)
        {
            var stored = await refreshTokenStore.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stored))
                return stored;

            return string.IsNullOrWhiteSpace(options.RefreshToken) ? null : options.RefreshToken;
        }

        private async Task<TokenResult> RequestAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token",
            });

            var httpClient = httpClientFactory.CreateClient(GmailOptions.HttpClientName);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new GmailTokenException("Failed to reach the Google token endpoint.", ex);
            }

            using (response)
            {
                string payload;
                try
                {
                    payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    throw new GmailTokenException("Failed to read the Google token response.", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = TryReadStringProperty(payload, "error") ?? "unknown_error";
                    if (error == "invalid_grant")
                    {
                        try
                        {
                            await refreshTokenStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            logger.LogWarning(ex, "Failed to delete the revoked Gmail refresh token from storage.");
                        }

                        throw new GmailAuthorizationRequiredException(
                            "The Gmail authorization has expired or was revoked. Open /oauth/google/start to authorize again.");
                    }

                    throw new GmailTokenException(
                        $"Gmail token refresh failed ({(int)response.StatusCode} {response.StatusCode}): {error}");
                }

                try
                {
                    using var document = JsonDocument.Parse(payload);
                    var root = document.RootElement;

                    var accessToken = TryReadStringProperty(root, "access_token");
                    if (string.IsNullOrWhiteSpace(accessToken))
                        throw new GmailTokenException("Google token response did not contain an access token.");

                    var expiresIn = root.TryGetProperty("expires_in", out var expiresElement) && expiresElement.TryGetInt32(out var seconds)
                        ? seconds
                        : 3600;

                    var rotatedRefreshToken = TryReadStringProperty(root, "refresh_token");
                    if (!string.IsNullOrWhiteSpace(rotatedRefreshToken))
                    {
                        try
                        {
                            await refreshTokenStore.WriteAsync(rotatedRefreshToken, cancellationToken).ConfigureAwait(false);
                            logger.LogInformation("Google returned a rotated Gmail refresh token; it has been saved.");
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
                        {
                            logger.LogError(ex, "Failed to persist the rotated Gmail refresh token.");
                        }
                    }

                    return new TokenResult(accessToken, expiresIn);
                }
                catch (JsonException ex)
                {
                    throw new GmailTokenException("Google token response was not valid JSON.", ex);
                }
            }
        }

        private void EnsureConfigured()
        {
            if (options.IsConfigured)
                return;

            throw new GmailNotConfiguredException(
                "Gmail is not configured. Missing or invalid: " + string.Join(", ", options.GetMissingSettings()));
        }

        private static string? TryReadStringProperty(string payload, string propertyName)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                return TryReadStringProperty(document.RootElement, propertyName);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? TryReadStringProperty(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);

        private sealed record TokenResult(string AccessToken, int ExpiresInSeconds);
    }
}
