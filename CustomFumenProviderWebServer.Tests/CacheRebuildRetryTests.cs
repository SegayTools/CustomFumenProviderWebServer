using System.Collections.Concurrent;
using CustomFumenProviderWebServer.Services.CacheManager;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class CacheRebuildRetryTests
{
    [Fact]
    public async Task RebuildUntilSuccessAsync_RetriesFailuresUntilSuccess()
    {
        var service = new FakeCacheListService(failuresBeforeSuccess: 2);

        await CacheRebuildRetry.RebuildUntilSuccessAsync(
            service,
            NullLogger.Instance,
            CancellationToken.None,
            TimeSpan.FromMilliseconds(1));

        Assert.Equal(3, service.ForceRebuildCalls);
    }

    [Fact]
    public async Task RebuildUntilSuccessAsync_StopsWhenCancelled()
    {
        using var cts = new CancellationTokenSource();
        var service = new FakeCacheListService(failuresBeforeSuccess: int.MaxValue, afterFailure: cts.Cancel);

        await CacheRebuildRetry.RebuildUntilSuccessAsync(
            service,
            NullLogger.Instance,
            cts.Token,
            TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, service.ForceRebuildCalls);
    }

    private sealed class FakeCacheListService : IFileCacheListService
    {
        private readonly int failuresBeforeSuccess;
        private readonly Action? afterFailure;

        public FakeCacheListService(int failuresBeforeSuccess, Action? afterFailure = null)
        {
            this.failuresBeforeSuccess = failuresBeforeSuccess;
            this.afterFailure = afterFailure;
        }

        public int ForceRebuildCalls { get; private set; }

        public ConcurrentDictionary<int, CacheFumenInfo> CacheFumenMap { get; } = new();

        public Task ForceRebuildAll()
        {
            ForceRebuildCalls++;
            if (ForceRebuildCalls <= failuresBeforeSuccess)
            {
                afterFailure?.Invoke();
                throw new InvalidOperationException("database unavailable");
            }

            return Task.CompletedTask;
        }

        public Task UpdateCacheFumenInfo(int musicId)
        {
            return Task.CompletedTask;
        }

        public string GetFumenFolderPath()
        {
            return Path.GetTempPath();
        }

        public Task ScanChanges(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
