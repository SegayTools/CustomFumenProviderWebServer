using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class GmailTokenStoreTests
{
    [Fact]
    public async Task WriteReadAndDelete_RoundTripsEncryptedToken()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new GmailTokenStore(
            options,
            new EphemeralDataProtectionProvider(),
            NullLogger<GmailTokenStore>.Instance);

        await store.WriteAsync("1//refresh-token");

        var tokenPath = Path.Combine(options.DataDirectory, "refresh_token.dat");
        var onDisk = await File.ReadAllTextAsync(tokenPath);
        Assert.DoesNotContain("1//refresh-token", onDisk);
        Assert.Equal("1//refresh-token", await store.ReadAsync());

        await store.DeleteAsync();
        Assert.Null(await store.ReadAsync());
        Assert.False(File.Exists(tokenPath));
    }

    [Fact]
    public async Task ReadAsync_CorruptedToken_ReturnsNull()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new GmailTokenStore(
            options,
            new EphemeralDataProtectionProvider(),
            NullLogger<GmailTokenStore>.Instance);

        await File.WriteAllTextAsync(Path.Combine(options.DataDirectory, "refresh_token.dat"), "not-a-protected-value");

        Assert.Null(await store.ReadAsync());
    }
}
