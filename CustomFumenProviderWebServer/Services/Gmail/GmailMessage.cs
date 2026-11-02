#nullable enable

namespace CustomFumenProviderWebServer.Services.Gmail
{
    public sealed class GmailMessage
    {
        public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Cc { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Bcc { get; init; } = Array.Empty<string>();

        public string Subject { get; init; } = string.Empty;

        public string? TextBody { get; init; }

        public string? HtmlBody { get; init; }

        public IReadOnlyList<GmailAttachment> Attachments { get; init; } = Array.Empty<GmailAttachment>();
    }

    public sealed class GmailAttachment
    {
        public string FileName { get; init; } = string.Empty;

        public byte[] Content { get; init; } = Array.Empty<byte>();

        public string ContentType { get; init; } = "application/octet-stream";
    }
}
