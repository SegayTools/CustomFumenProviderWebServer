#nullable enable

namespace CustomFumenProviderWebServer.Services.Email
{
    public sealed class EmailMessage
    {
        public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();

        public string Subject { get; init; } = string.Empty;

        public string? TextBody { get; init; }

        public string? HtmlBody { get; init; }

        public IReadOnlyList<EmailAttachment> Attachments { get; init; } = Array.Empty<EmailAttachment>();
    }

    public sealed class EmailAttachment
    {
        public string FileName { get; init; } = string.Empty;

        public byte[] Content { get; init; } = Array.Empty<byte>();

        public string ContentType { get; init; } = "application/octet-stream";
    }
}
