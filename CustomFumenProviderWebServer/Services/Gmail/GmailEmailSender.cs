#nullable enable

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using MimeKit;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    /// <summary>
    /// 通过 Gmail REST API 发送邮件。业务代码只需要构造 <see cref="GmailMessage"/>。
    /// </summary>
    public sealed class GmailEmailSender
    {
        private const string SendEndpoint = "https://gmail.googleapis.com/gmail/v1/users/me/messages/send";

        private readonly IHttpClientFactory httpClientFactory;
        private readonly IGmailAccessTokenProvider accessTokenProvider;
        private readonly IGmailAccountStore accountStore;
        private readonly GmailOptions options;
        private readonly TimeProvider timeProvider;

        public GmailEmailSender(
            IHttpClientFactory httpClientFactory,
            IGmailAccessTokenProvider accessTokenProvider,
            IGmailAccountStore accountStore,
            GmailOptions options,
            TimeProvider timeProvider)
        {
            this.httpClientFactory = httpClientFactory;
            this.accessTokenProvider = accessTokenProvider;
            this.accountStore = accountStore;
            this.options = options;
            this.timeProvider = timeProvider;
        }

        public async Task<string?> SendAsync(GmailMessage email, CancellationToken cancellationToken = default)
        {
            if (email is null)
                throw new ArgumentNullException(nameof(email));
            if (email.To.Count == 0)
                throw new ArgumentException("At least one recipient is required.", nameof(email));

            EnsureConfigured();

            var accessToken = await accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var senderAddress = await ResolveSenderAddressAsync(cancellationToken).ConfigureAwait(false);
            var rawMessage = await BuildRawMessageAsync(email, senderAddress, cancellationToken).ConfigureAwait(false);

            using (var firstAttempt = await SendRawMessageAsync(rawMessage, accessToken, cancellationToken).ConfigureAwait(false))
            {
                if (firstAttempt.StatusCode != HttpStatusCode.Unauthorized)
                    return await ReadMessageIdOrThrowAsync(firstAttempt, cancellationToken).ConfigureAwait(false);
            }

            // Access Token 可能刚过期或已被撤销，刷新后只重试一次。
            accessTokenProvider.Invalidate();
            accessToken = await accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            using var retry = await SendRawMessageAsync(rawMessage, accessToken, cancellationToken).ConfigureAwait(false);
            return await ReadMessageIdOrThrowAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        private async Task<string> BuildRawMessageAsync(GmailMessage email, string senderAddress, CancellationToken cancellationToken)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(senderAddress));

            foreach (var address in email.To)
                message.To.Add(MailboxAddress.Parse(address));
            foreach (var address in email.Cc)
                message.Cc.Add(MailboxAddress.Parse(address));
            foreach (var address in email.Bcc)
                message.Bcc.Add(MailboxAddress.Parse(address));

            message.Subject = email.Subject;
            message.Date = timeProvider.GetUtcNow();

            var body = new BodyBuilder
            {
                TextBody = email.TextBody ?? string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(email.HtmlBody))
                body.HtmlBody = email.HtmlBody;

            foreach (var attachment in email.Attachments)
            {
                body.Attachments.Add(
                    attachment.FileName,
                    attachment.Content,
                    ContentType.Parse(attachment.ContentType));
            }

            message.Body = body.ToMessageBody();

            using var stream = new MemoryStream();
            await message.WriteToAsync(stream, cancellationToken).ConfigureAwait(false);

            return WebEncoders.Base64UrlEncode(stream.GetBuffer().AsSpan(0, (int)stream.Length));
        }

        private async Task<HttpResponseMessage> SendRawMessageAsync(string rawMessage, string accessToken, CancellationToken cancellationToken)
        {
            var payload = JsonSerializer.Serialize(new { raw = rawMessage });
            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var httpClient = httpClientFactory.CreateClient(GmailOptions.HttpClientName);
            try
            {
                return await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new GmailSendException(HttpStatusCode.ServiceUnavailable, "Failed to reach the Gmail API: " + ex.Message);
            }
        }

        private static async Task<string?> ReadMessageIdOrThrowAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new GmailSendException(response.StatusCode, ReadErrorMessage(payload, response.StatusCode));

            try
            {
                using var document = JsonDocument.Parse(payload);
                return document.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
                    ? id.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string ReadErrorMessage(string payload, HttpStatusCode statusCode)
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty("error", out var error) &&
                    error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                {
                    return $"Gmail API rejected the message ({(int)statusCode} {statusCode}): {message.GetString()}";
                }
            }
            catch (JsonException)
            {
                // Fall through to a bounded raw response.
            }

            var detail = payload.Length > 500 ? payload[..500] : payload;
            return $"Gmail API rejected the message ({(int)statusCode} {statusCode}): {detail}";
        }

        private async Task<string> ResolveSenderAddressAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(options.SenderAddress))
                return options.SenderAddress;

            var storedAddress = await accountStore.ReadSenderAddressAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(storedAddress))
                return storedAddress;

            throw new GmailAuthorizationRequiredException(
                "Gmail sender address is unknown. Complete the Google OAuth authorization or set GMAIL_SENDER_ADDRESS.");
        }

        private void EnsureConfigured()
        {
            if (options.IsConfigured)
                return;

            throw new GmailNotConfiguredException(
                "Gmail is not configured. Missing or invalid: " + string.Join(", ", options.GetMissingSettings()));
        }
    }
}
