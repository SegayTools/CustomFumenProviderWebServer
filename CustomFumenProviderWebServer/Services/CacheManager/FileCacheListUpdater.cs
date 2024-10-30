
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
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly IFileCacheListService cacheListService;
        private readonly string fumenFolderPath;

        public FileCacheListUpdater(ILogger<FileCacheListUpdater> logger, IDbContextFactory<FumenDataDB> fumenDataDBFactory, IFileCacheListService cacheListService)
        {
            this.logger = logger;
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.cacheListService = cacheListService;
            fumenFolderPath = Environment.GetEnvironmentVariable("FumenDirectory");
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
            await Initalize();

            while (!cancelTokenSource.IsCancellationRequested)
            {
                try
                {
                    await OnUpdateInternal(token);
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"call OnUpdateInternal() failed: {e.Message}");
                }
            }
        }

        private async Task Initalize()
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSets = await db.FumenSets.Where(x => x.PublishState == PublishState.Published).ToArrayAsync();

            cacheListService.CacheFumenMap.Clear();

            //一次性构造所有文件
            foreach (var set in fumenSets)
            {
                var fumenInfo = BuildCacheFumenInfo(set);
                cacheListService.CacheFumenMap[fumenInfo.MusicId] = fumenInfo;

                logger.LogInformation($"initalize CacheFumenInfo: {fumenInfo.MusicId}");
            }
        }

        private CacheFumenInfo BuildCacheFumenInfo(FumenSet set)
        {
            var fumenInfo = new CacheFumenInfo()
            {
                MusicId = set.MusicId,
                UpdateTime = set.UpdateTime,
            };
            var optFolder = Path.Combine(fumenFolderPath, $"fumen{set.MusicId}", "opt");
            fumenInfo.CacheFileInfo = Build(optFolder, fumenFolderPath);
            return fumenInfo;
        }

        private List<CacheFileInfo> Build(string optFolder, string relativeFolder)
        {
            var list = new List<CacheFileInfo>();
            foreach (var file in Directory.GetFiles(optFolder, "*.*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(relativeFolder, file);
                var time = File.GetLastWriteTimeUtc(file);

                var info = new CacheFileInfo()
                {
                    RelativeFilePath = relativePath,
                    LastWriteTime = time
                };
                list.Add(info);
            }
            return list;
        }

        private async Task OnUpdateInternal(CancellationToken token)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSets = await db.FumenSets.Where(x => x.PublishState == PublishState.Published).ToArrayAsync();

            foreach (var set in fumenSets)
            {
                var isBuild = false;
                if (cacheListService.CacheFumenMap.TryGetValue(set.MusicId, out var cacheFumenInfo))
                {
                    if (cacheFumenInfo.UpdateTime < set.UpdateTime)
                        isBuild = true;
                }
                else
                    isBuild = true;

                if (isBuild)
                {
                    cacheFumenInfo = BuildCacheFumenInfo(set);
                    cacheListService.CacheFumenMap[set.MusicId] = cacheFumenInfo;
                    logger.LogInformation($"replace CacheFumenInfo: {set.MusicId}");
                }
            }

            //delete unused
            var hash = fumenSets.Select(x => x.MusicId).ToHashSet();
            foreach (var musicId in cacheListService.CacheFumenMap.Keys.ToArray())
            {
                if (!hash.Contains(musicId))
                {
                    cacheListService.CacheFumenMap.Remove(musicId, out _);
                    logger.LogInformation($"remove unused CacheFumenInfo: {musicId}");
                }
            }

            logger.LogInformation($"OnUpdateInternal() done");
        }
    }
}
