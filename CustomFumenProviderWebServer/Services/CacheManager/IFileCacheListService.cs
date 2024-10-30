using System.Collections.Concurrent;

namespace CustomFumenProviderWebServer.Services.CacheManager
{
    public interface IFileCacheListService
    {
        ConcurrentDictionary<int, CacheFumenInfo> CacheFumenMap { get; }
    }
}
