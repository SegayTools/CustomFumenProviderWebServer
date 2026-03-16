
using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Migrations;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.CacheManager;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace CustomFumenProviderWebServer.Services.FileCacheList
{
    public class FileCacheListUpdater : IHostedService
    {
        private CancellationTokenSource cancelTokenSource;
        private readonly ILogger<FileCacheListUpdater> logger;
        private readonly IFileCacheListService cacheListService;

        public FileCacheListUpdater(ILogger<FileCacheListUpdater> logger, IFileCacheListService cacheListService)
        {
            this.logger = logger;
            this.cacheListService = cacheListService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancelTokenSource = new CancellationTokenSource();
            Task.Run(() => OnUpdate(cancelTokenSource.Token), cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            cancelTokenSource.Cancel();
            return Task.CompletedTask;
        }

        private async void OnUpdate(CancellationToken token)
        {
            await cacheListService.ForceRebuildAll();

            while (!cancelTokenSource.IsCancellationRequested)
            {
                try
                {
                    await cacheListService.ScanChanges(token);
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"call OnUpdateInternal() failed: {e.Message}");
                }
            }
        }
    }
}
