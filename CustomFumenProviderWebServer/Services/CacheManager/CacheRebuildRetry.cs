using Microsoft.Extensions.Logging;

namespace CustomFumenProviderWebServer.Services.CacheManager
{
    public static class CacheRebuildRetry
    {
        /// <summary>
        /// 启动时首次全量重建可能遇到数据库/磁盘暂时不可用。
        /// 持续重试，避免后台任务直接 faulted 导致缓存永久为空。
        /// </summary>
        public static async Task RebuildUntilSuccessAsync(
            IFileCacheListService cacheListService,
            ILogger logger,
            CancellationToken token,
            TimeSpan? retryDelay = null)
        {
            var delay = retryDelay ?? TimeSpan.FromSeconds(5);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await cacheListService.ForceRebuildAll();
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "rebuild cache list failed, retry in {Delay}", delay);
                    try
                    {
                        await Task.Delay(delay, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }
    }
}
