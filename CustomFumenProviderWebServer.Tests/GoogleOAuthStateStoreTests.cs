using CustomFumenProviderWebServer.Services.Gmail;

namespace CustomFumenProviderWebServer.Tests;

public class GoogleOAuthStateStoreTests
{
    [Fact]
    public void State_CanOnlyBeConsumedOnce()
    {
        var store = new GoogleOAuthStateStore(TimeProvider.System);

        var state = store.Create();

        Assert.False(string.IsNullOrWhiteSpace(state));
        Assert.True(store.TryConsume(state));
        Assert.False(store.TryConsume(state));
    }

    [Fact]
    public void State_ExpiresAfterTenMinutes()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new GoogleOAuthStateStore(time);

        var state = store.Create();
        time.Advance(TimeSpan.FromMinutes(11));

        Assert.False(store.TryConsume(state));
    }

    [Fact]
    public void UnknownOrMissingState_IsRejected()
    {
        var store = new GoogleOAuthStateStore(TimeProvider.System);

        Assert.False(store.TryConsume("unknown"));
        Assert.False(store.TryConsume(null));
    }
}
