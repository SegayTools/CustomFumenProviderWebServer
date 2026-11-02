#nullable enable

using MimeKit;

namespace CustomFumenProviderWebServer.Services.Email
{
    public interface IEmailTransport
    {
        Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default);
    }

    public sealed class EmailNotConfiguredException : Exception
    {
        public EmailNotConfiguredException(string message) : base(message)
        {
        }
    }

    public sealed class EmailSendException : Exception
    {
        public EmailSendException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
