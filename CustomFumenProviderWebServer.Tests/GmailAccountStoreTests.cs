using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class GmailAccountStoreTests
{
    [Fact]
    public async Task WriteRead_RoundTripsSenderAddress()
    {
        var options = GmailTestHelpers.CreateOptions(senderAddress: "");
        var store = new GmailAccountStore(options, NullLogger<GmailAccountStore>.Instance);

        await store.WriteSenderAddressAsync("authorized@example.com");

        Assert.Equal("authorized@example.com", await store.ReadSenderAddressAsync());
        Assert.Equal(
            "authorized@example.com",
            (await File.ReadAllTextAsync(Path.Combine(options.DataDirectory, "sender_address.txt"))).Trim());
    }

    [Fact]
    public async Task WriteSenderAddress_InvalidAddress_Throws()
    {
        var options = GmailTestHelpers.CreateOptions(senderAddress: "");
        var store = new GmailAccountStore(options, NullLogger<GmailAccountStore>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() => store.WriteSenderAddressAsync("not-an-email"));
    }

    [Fact]
    public async Task ReadSenderAddress_InvalidFileContent_ReturnsNull()
    {
        var options = GmailTestHelpers.CreateOptions(senderAddress: "");
        var store = new GmailAccountStore(options, NullLogger<GmailAccountStore>.Instance);
        await File.WriteAllTextAsync(Path.Combine(options.DataDirectory, "sender_address.txt"), "broken");

        Assert.Null(await store.ReadSenderAddressAsync());
    }
}
