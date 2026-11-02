#nullable enable

using MailKit.Net.Smtp;
using MimeKit;

namespace CustomFumenProviderWebServer.Services.Email
{
    /// <summary>
    /// 使用 MailKit 建立 SMTP 连接并发送 MIME 邮件。
    /// 每次发送使用独立连接，避免 SmtpClient 的线程安全问题。
    /// </summary>
    public sealed class SmtpEmailTransport : IEmailTransport
    {
        private const int TimeoutMilliseconds = 30_000;

        private readonly EmailOptions options;
        private readonly ILogger<SmtpEmailTransport> logger;

        public SmtpEmailTransport(EmailOptions options, ILogger<SmtpEmailTransport> logger)
        {
            this.options = options;
            this.logger = logger;
        }

        public async Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
        {
            using var client = new SmtpClient
            {
                Timeout = TimeoutMilliseconds,
            };

            await client.ConnectAsync(
                options.Host,
                options.Port,
                options.GetSecureSocketOptions(),
                cancellationToken).ConfigureAwait(false);
            await client.AuthenticateAsync(
                options.Username,
                options.Password,
                cancellationToken).ConfigureAwait(false);
            await client.SendAsync(message, cancellationToken).ConfigureAwait(false);

            try
            {
                await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 邮件已经提交成功，QUIT 失败不应让调用方误以为发送失败。
                logger.LogWarning(ex, "SMTP QUIT failed after the message was sent.");
            }
        }
    }
}
