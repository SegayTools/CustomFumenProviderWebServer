using CustomFumenProviderWebServer.Services.Email;
using MailKit.Security;

namespace CustomFumenProviderWebServer.Tests;

public class EmailOptionsTests
{
    [Fact]
    public void IsConfigured_RequiresCredentialsAndValidSender()
    {
        Assert.True(EmailTestHelpers.CreateOptions().IsConfigured);
        Assert.False(EmailTestHelpers.CreateOptions(password: "").IsConfigured);
        Assert.False(EmailTestHelpers.CreateOptions(username: "").IsConfigured);
        Assert.False(EmailTestHelpers.CreateOptions(fromAddress: "not-an-email").IsConfigured);
        Assert.False(EmailTestHelpers.CreateOptions(security: "invalid").IsConfigured);
        Assert.False(EmailTestHelpers.CreateOptions(port: -1).IsConfigured);
    }

    [Theory]
    [InlineData("ssl", SecureSocketOptions.SslOnConnect)]
    [InlineData("starttls", SecureSocketOptions.StartTls)]
    [InlineData("none", SecureSocketOptions.None)]
    [InlineData("auto", SecureSocketOptions.Auto)]
    public void GetSecureSocketOptions_MapsConfiguredSecurity(string security, SecureSocketOptions expected)
    {
        var options = EmailTestHelpers.CreateOptions(security: security);

        Assert.Equal(expected, options.GetSecureSocketOptions());
    }

    [Fact]
    public void GetSecureSocketOptions_IsCaseInsensitive()
    {
        var options = EmailTestHelpers.CreateOptions(security: "SSL");

        Assert.Equal(SecureSocketOptions.SslOnConnect, options.GetSecureSocketOptions());
    }
}
