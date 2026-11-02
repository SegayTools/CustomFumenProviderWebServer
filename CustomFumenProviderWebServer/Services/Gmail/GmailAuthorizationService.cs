#nullable enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    /// <summary>
    /// Google OAuth 2.0 Web Server Flow。
    /// 申请 openid/email 以自动识别发件账户，并申请 gmail.send 以发送邮件。
    /// 使用 offline + consent 确保返回 Refresh Token。
    /// </summary>
    public sealed class GmailAuthorizationService
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

        private readonly IHttpClientFactory httpClientFactory;
        private readonly IGmailRefreshTokenStore refreshTokenStore;
        private readonly IGmailAccountStore accountStore;
        private readonly GmailOptions options;
        private readonly ILogger<GmailAuthorizationService> logger;

        public GmailAuthorizationService(
            IHttpClientFactory httpClientFactory,
            IGmailRefreshTokenStore refreshTokenStore,
            IGmailAccountStore accountStore,
            GmailOptions options,
            ILogger<GmailAuthorizationService> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.refreshTokenStore = refreshTokenStore;
            this.accountStore = accountStore;
            this.options = options;
            this.logger = logger;
        }

        public Uri CreateAuthorizationUri(string state)
        {
            EnsureConfigured();

            var url = QueryHelpers.AddQueryString(AuthorizationEndpoint, new Dictionary<string, string?>
            {
                ["client_id"] = options.ClientId,
                ["redirect_uri"] = options.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = GmailOptions.OAuthScope,
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["state"] = state,
            });

            return new Uri(url, UriKind.Absolute);
        }

        public async Task ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = options.RedirectUri,
            });

            var httpClient = httpClientFactory.CreateClient(GmailOptions.HttpClientName);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsync(TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new GmailAuthorizationException("Failed to reach the Google token endpoint.", ex);
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
                    throw new GmailAuthorizationException("Failed to read the Google token response.", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = TryReadStringProperty(payload, "error") ?? "unknown_error";
                    throw new GmailAuthorizationException(
                        $"Google OAuth token exchange failed ({(int)response.StatusCode} {response.StatusCode}): {error}");
                }

                string? refreshToken;
                string? idToken;
                try
                {
                    using var document = JsonDocument.Parse(payload);
                    refreshToken = TryReadStringProperty(document.RootElement, "refresh_token");
                    idToken = TryReadStringProperty(document.RootElement, "id_token");
                }
                catch (JsonException ex)
                {
                    throw new GmailAuthorizationException("Google OAuth token response was not valid JSON.", ex);
                }

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    throw new GmailAuthorizationException(
                        "Google did not return a refresh token. Revoke this application's access in the Google Account permissions page, then authorize again.");
                }

                try
                {
                    await refreshTokenStore.WriteAsync(refreshToken, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
                {
                    throw new GmailAuthorizationException(
                        "Failed to save the Gmail refresh token to persistent storage. Check GMAIL_DATA_DIR permissions.",
                        ex);
                }

                logger.LogInformation("Google OAuth authorization completed and the Gmail refresh token was saved.");
                await SaveSenderAddressAsync(idToken, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SaveSenderAddressAsync(string? idToken, CancellationToken cancellationToken)
        {
            var senderAddress = TryReadEmailFromIdToken(idToken);
            if (string.IsNullOrWhiteSpace(senderAddress))
            {
                logger.LogWarning(
                    "Google OAuth response did not contain an email claim. Set GMAIL_SENDER_ADDRESS if sending fails.");
                return;
            }

            try
            {
                await accountStore.WriteSenderAddressAsync(senderAddress, cancellationToken).ConfigureAwait(false);
                logger.LogInformation("Saved the authorized Gmail sender address {SenderAddress}.", senderAddress);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "Failed to persist the authorized Gmail sender address.");
            }
        }

        private string? TryReadEmailFromIdToken(string? idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                return null;

            var parts = idToken.Split('.');
            if (parts.Length != 3)
                return null;

            try
            {
                var payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[1]));
                using var document = JsonDocument.Parse(payload);
                var root = document.RootElement;

                var audience = TryReadStringProperty(root, "aud");
                if (!string.IsNullOrWhiteSpace(audience) && audience != options.ClientId)
                {
                    logger.LogWarning("The Google ID token audience does not match the configured client ID; ignoring it.");
                    return null;
                }

                var issuer = TryReadStringProperty(root, "iss");
                if (!string.IsNullOrWhiteSpace(issuer) &&
                    issuer != "https://accounts.google.com" &&
                    issuer != "accounts.google.com")
                {
                    logger.LogWarning("The Google ID token issuer is unexpected; ignoring it.");
                    return null;
                }

                var email = TryReadStringProperty(root, "email");
                return string.IsNullOrWhiteSpace(email) ? null : email;
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                logger.LogWarning(ex, "Failed to parse the Google ID token while reading the sender address.");
                return null;
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
    }
}
