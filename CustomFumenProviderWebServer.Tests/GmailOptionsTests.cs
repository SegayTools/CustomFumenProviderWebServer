using CustomFumenProviderWebServer.Services.Gmail;

namespace CustomFumenProviderWebServer.Tests;

public class GmailOptionsTests
{
    [Fact]
    public void IsConfigured_RequiresCompleteAndValidCoreSettings()
    {
        Assert.True(Create().IsConfigured);
        Assert.True(Create(senderAddress: "").IsConfigured);

        Assert.False(Create(clientSecret: "").IsConfigured);
        Assert.False(Create(redirectUri: "https://example.com/wrong/callback").IsConfigured);
        Assert.False(Create(redirectUri: "http://example.com/oauth/google/callback").IsConfigured);
        Assert.False(Create(senderAddress: "not-an-email").IsConfigured);

        Assert.DoesNotContain(
            Create(senderAddress: "").GetMissingSettings(),
            setting => setting.Contains("GMAIL_SENDER_ADDRESS", StringComparison.Ordinal));
    }

    [Fact]
    public void IsConfigured_AllowsHttpOnlyOnLocalhost()
    {
        Assert.True(Create(redirectUri: "http://localhost:8080/oauth/google/callback").IsConfigured);
    }

    private static GmailOptions Create(
        string clientId = "test-client-id",
        string clientSecret = "test-client-secret",
        string redirectUri = "https://example.com/oauth/google/callback",
        string senderAddress = "sender@example.com")
    {
        return new GmailOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            RedirectUri = redirectUri,
            SenderAddress = senderAddress,
            DataDirectory = GmailTestHelpers.CreateTempDirectory(),
        };
    }
}
