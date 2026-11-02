#nullable enable

using MimeKit;
using MimeKit.Utils;

namespace CustomFumenProviderWebServer.Services.Email
{
    /// <summary>
    /// 构造 MIME 邮件并通过 SMTP 发送。业务代码只需要构造 <see cref="EmailMessage"/>。
    /// </summary>
    public sealed class SmtpEmailSender
    {
        private readonly EmailOptions options;
        private readonly IEmailTransport transport;

        public SmtpEmailSender(EmailOptions options, IEmailTransport transport)
        {
            this.options = options;
            this.transport = transport;
        }

        public async Task<string> SendAsync(EmailMessage email, CancellationToken cancellationToken = default)
        {
            if (email is null)
                throw new ArgumentNullException(nameof(email));
            if (email.To.Count == 0)
                throw new ArgumentException("At least one recipient is required.", nameof(email));

            EnsureConfigured();

            var mimeMessage = BuildMessage(email);

            try
            {
                await transport.SendAsync(mimeMessage, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new EmailSendException("SMTP send failed: " + ex.Message, ex);
            }

            return mimeMessage.MessageId;
        }

        private MimeMessage BuildMessage(EmailMessage email)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));

            foreach (var address in email.To)
                message.To.Add(MailboxAddress.Parse(address));
            foreach (var address in email.Cc)
                message.Cc.Add(MailboxAddress.Parse(address));
            foreach (var address in email.Bcc)
                message.Bcc.Add(MailboxAddress.Parse(address));

            message.Subject = email.Subject;
            message.Date = DateTimeOffset.UtcNow;
            message.MessageId = MimeUtils.GenerateMessageId();

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
            return message;
        }

        private void EnsureConfigured()
        {
            if (options.IsConfigured)
                return;

            throw new EmailNotConfiguredException(
                "SMTP email is not configured. Missing or invalid: " + string.Join(", ", options.GetMissingSettings()));
        }
    }
}
