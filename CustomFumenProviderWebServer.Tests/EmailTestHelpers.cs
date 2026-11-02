#nullable enable

using CustomFumenProviderWebServer.Services.Email;
using MimeKit;

namespace CustomFumenProviderWebServer.Tests;

internal static class EmailTestHelpers
{
    public static EmailOptions CreateOptions(
        string username = "sender@126.com",
        string password = "auth-code",
        string fromAddress = "sender@126.com",
        string security = "ssl",
        int port = 465)
    {
        return new EmailOptions
        {
            Host = "smtp.126.com",
            Port = port,
            Security = security,
            Username = username,
            Password = password,
            FromAddress = fromAddress,
            FromName = "Test Sender",
        };
    }

    public static ActivityReportOptions CreateReportOptions()
    {
        return new ActivityReportOptions
        {
            Enabled = true,
            Recipients = new[] { "report@example.com" },
            IntervalHours = 6,
            UtcOffsetHours = 8,
            MaxRows = 500,
            SiteUrl = "https://example.com",
        };
    }
}

internal sealed class FakeEmailTransport : IEmailTransport
{
    public MimeMessage? LastMessage { get; private set; }

    public Exception? ExceptionToThrow { get; set; }

    public Task SendAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        LastMessage = message;
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;
        return Task.CompletedTask;
    }
}
