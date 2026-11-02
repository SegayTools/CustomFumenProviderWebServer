using CustomFumenProviderWebServer.Services.Email;
using MimeKit;

namespace CustomFumenProviderWebServer.Tests;

public class SmtpEmailSenderTests
{
    [Fact]
    public async Task SendAsync_BuildsMimeMessage()
    {
        var transport = new FakeEmailTransport();
        var sender = new SmtpEmailSender(EmailTestHelpers.CreateOptions(), transport);

        var messageId = await sender.SendAsync(new EmailMessage
        {
            To = new[] { "receiver@example.com" },
            Cc = new[] { "cc@example.com" },
            Subject = "hello",
            TextBody = "world",
        });

        Assert.False(string.IsNullOrWhiteSpace(messageId));
        Assert.NotNull(transport.LastMessage);

        var message = transport.LastMessage;
        Assert.Equal("sender@126.com", message.From.Mailboxes.Single().Address);
        Assert.Equal("receiver@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("cc@example.com", message.Cc.Mailboxes.Single().Address);
        Assert.Equal("hello", message.Subject);
        Assert.Equal("world", message.TextBody!.Trim());
        Assert.Equal(messageId, message.MessageId);
    }

    [Fact]
    public async Task SendAsync_WithAttachment_AddsAttachment()
    {
        var transport = new FakeEmailTransport();
        var sender = new SmtpEmailSender(EmailTestHelpers.CreateOptions(), transport);

        await sender.SendAsync(new EmailMessage
        {
            To = new[] { "receiver@example.com" },
            Subject = "attachment",
            TextBody = "see attachment",
            Attachments = new[]
            {
                new EmailAttachment
                {
                    FileName = "test.txt",
                    Content = "attachment-content"u8.ToArray(),
                    ContentType = "text/plain",
                },
            },
        });

        Assert.NotNull(transport.LastMessage);
        var attachment = Assert.Single(transport.LastMessage.Attachments);
        Assert.Equal("test.txt", attachment.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task SendAsync_WhenNotConfigured_Throws()
    {
        var sender = new SmtpEmailSender(EmailTestHelpers.CreateOptions(password: ""), new FakeEmailTransport());

        await Assert.ThrowsAsync<EmailNotConfiguredException>(() => sender.SendAsync(new EmailMessage
        {
            To = new[] { "receiver@example.com" },
        }));
    }

    [Fact]
    public async Task SendAsync_WhenTransportFails_WrapsException()
    {
        var transport = new FakeEmailTransport
        {
            ExceptionToThrow = new InvalidOperationException("smtp rejected"),
        };
        var sender = new SmtpEmailSender(EmailTestHelpers.CreateOptions(), transport);

        var exception = await Assert.ThrowsAsync<EmailSendException>(() => sender.SendAsync(new EmailMessage
        {
            To = new[] { "receiver@example.com" },
        }));

        Assert.Contains("smtp rejected", exception.Message);
    }

    [Fact]
    public async Task SendAsync_WithoutRecipients_Throws()
    {
        var sender = new SmtpEmailSender(EmailTestHelpers.CreateOptions(), new FakeEmailTransport());

        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(new EmailMessage()));
    }
}
