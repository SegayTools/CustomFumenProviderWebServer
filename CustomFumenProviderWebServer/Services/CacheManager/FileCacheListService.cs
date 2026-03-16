using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.FileCacheList;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace CustomFumenProviderWebServer.Services.CacheManager
{
    public class FileCacheListService : IFileCacheListService
    {
        private readonly ILogger<FileCacheListService> logger;
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly string fumenFolderPath;

        public ConcurrentDictionary<int, CacheFumenInfo> CacheFumenMap { get; } = new();

        public FileCacheListService(ILogger<FileCacheListService> logger, IDbContextFactory<FumenDataDB> fumenDataDBFactory)
        {
            this.logger = logger;
            this.fumenDataDBFactory = fumenDataDBFactory;
            fumenFolderPath = Environment.GetEnvironmentVariable("FumenDirectory");
        }

        public async Task ForceRebuildAll()
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSets = await db.FumenSets.ToArrayAsync();

            CacheFumenMap.Clear();

            //一次性构造所有文件
            foreach (var set in fumenSets)
            {
                var fumenInfo = BuildCacheFumenInfo(set);
                CacheFumenMap[fumenInfo.MusicId] = fumenInfo;

                logger.LogInformation($"initalize CacheFumenInfo: {fumenInfo.MusicId}");
            }
        }


        private CacheFumenInfo BuildCacheFumenInfo(FumenSet set)
        {
            var fumenInfo = new CacheFumenInfo()
            {
                MusicId = set.MusicId,
                FumenSet = set,
                UpdateTime = set.UpdateTime,
            };
            var optFolder = Path.Combine(fumenFolderPath, $"fumen{set.MusicId}", "opt");
            fumenInfo.CacheFileInfo = Build(optFolder, fumenFolderPath);
            logger.LogInformation($"build CacheFumenInfo: {fumenInfo.MusicId}, opt file count: {fumenInfo.CacheFileInfo.Count}");
            return fumenInfo;
        }

        private List<CacheFileInfo> Build(string optFolder, string relativeFolder)
        {
            var list = new List<CacheFileInfo>();
            if (Directory.Exists(optFolder))
            {
                foreach (var file in Directory.GetFiles(optFolder, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(relativeFolder, file);
                    var time = File.GetLastWriteTimeUtc(file);
                    logger.LogInformation($"find cache file: {relativePath}, last write time: {time}");

                    var info = new CacheFileInfo()
                    {
                        RelativeFilePath = relativePath,
                        LastWriteTime = time
                    };
                    list.Add(info);
                }
            }
            else
            {
                logger.LogWarning($"opt folder not exist: {optFolder}");
            }
            return list;
        }

        public async Task ScanChanges(CancellationToken token)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSets = await db.FumenSets.ToArrayAsync();

            foreach (var set in fumenSets)
            {
                var isBuild = false;
                if (CacheFumenMap.TryGetValue(set.MusicId, out var cacheFumenInfo))
                {
                    if (cacheFumenInfo.UpdateTime != set.UpdateTime)
                        isBuild = true;
                }
                else
                    isBuild = true;

                if (isBuild)
                {
                    cacheFumenInfo = BuildCacheFumenInfo(set);
                    CacheFumenMap[set.MusicId] = cacheFumenInfo;
                    logger.LogInformation($"replace CacheFumenInfo: {set.MusicId}");
                }
            }

            //delete unused
            var hash = fumenSets.Select(x => x.MusicId).ToHashSet();
            foreach (var musicId in CacheFumenMap.Keys.ToArray())
            {
                if (!hash.Contains(musicId))
                {
                    CacheFumenMap.Remove(musicId, out _);
                    logger.LogInformation($"remove unused CacheFumenInfo: {musicId}");
                }
            }

            logger.LogInformation($"OnUpdateInternal() done");
        }
    }
}
