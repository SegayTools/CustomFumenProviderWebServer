using System.Collections.Concurrent;

namespace CustomFumenProviderWebServer.Services.CacheManager
{
    public class FileCacheListService : IFileCacheListService
    {
        public ConcurrentDictionary<int, CacheFumenInfo> CacheFumenMap { get; } = new();
    }
}
