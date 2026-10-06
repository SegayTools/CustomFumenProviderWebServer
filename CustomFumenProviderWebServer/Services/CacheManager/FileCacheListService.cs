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
            fumenFolderPath = Path.GetFullPath(Environment.GetEnvironmentVariable("FumenDirectory"));
            logger.LogInformation($"fumenFolderPath full path:{fumenFolderPath}");
        }

        public async Task ForceRebuildAll()
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSets = await db.FumenSets.Include(x => x.FumenDifficults).Where(x => x.PublishState != Models.PublishState.NotReadyForPending).ToArrayAsync();

            CacheFumenMap.Clear();

            //一次性构造所有文件
            foreach (var set in fumenSets)
            {
                var fumenInfo = BuildCacheFumenInfo(set);
                CacheFumenMap[fumenInfo.MusicId] = fumenInfo;

                logger.LogInformation($"initalize CacheFumenInfo: {fumenInfo.MusicId}");
            }
        }

        public async Task UpdateCacheFumenInfo(int musicId)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSet = await db.FumenSets.Include(x => x.FumenDifficults).FirstOrDefaultAsync(x => x.MusicId == musicId);

            if (fumenSet == null)
                return;

            var fumenInfo = BuildCacheFumenInfo(fumenSet);
            CacheFumenMap[fumenInfo.MusicId] = fumenInfo;

            logger.LogInformation($"update CacheFumenInfo: {fumenInfo.MusicId}");
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

        private List<CacheFileInfo> Build(string optFolder, string relativeFolder, bool writeLog = true)
        {
            var list = new List<CacheFileInfo>();
            if (Directory.Exists(optFolder))
            {
                foreach (var file in Directory.GetFiles(optFolder, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(relativeFolder, file);
                    var time = File.GetLastWriteTimeUtc(file);
                    if (writeLog)
                        logger.LogInformation($"find cache file: {relativePath}, last write time: {time}");

                    var info = new CacheFileInfo()
                    {
                        RelativeFilePath = relativePath,
                        LastWriteTime = time
                    };
                    list.Add(info);
                }
            }
            else if (writeLog)
            {
                logger.LogWarning($"opt folder not exist: {optFolder}");
            }
            return list;
        }

        /// <summary>
        /// 比较缓存里的文件列表与磁盘现状。用于发现"绕过 API 直接改盘"的文件变更。
        /// </summary>
        private bool IsSameFileList(CacheFumenInfo cacheFumenInfo, FumenSet set)
        {
            var optFolder = Path.Combine(fumenFolderPath, $"fumen{set.MusicId}", "opt");
            var currentFiles = Build(optFolder, fumenFolderPath, writeLog: false);

            if (currentFiles.Count != cacheFumenInfo.CacheFileInfo.Count)
                return false;

            for (int i = 0; i < currentFiles.Count; i++)
            {
                var current = currentFiles[i];
                var cached = cacheFumenInfo.CacheFileInfo[i];

                if (current.RelativeFilePath != cached.RelativeFilePath || current.LastWriteTime != cached.LastWriteTime)
                    return false;
            }

            return true;
        }

        public async Task ScanChanges(CancellationToken token)
        {
            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var fumenSets = await db.FumenSets.Include(x => x.FumenDifficults).Where(x => x.PublishState != Models.PublishState.NotReadyForPending).ToArrayAsync();

            foreach (var set in fumenSets)
            {
                var isBuild = false;
                if (CacheFumenMap.TryGetValue(set.MusicId, out var cacheFumenInfo))
                {
                    if (cacheFumenInfo.UpdateTime != set.UpdateTime)
                        isBuild = true;
                    else if (!IsSameFileList(cacheFumenInfo, set))
                    {
                        //数据库 UpdateTime 没变,但磁盘内容被直接改过(没过 API / 监听漏了事件)
                        isBuild = true;
                        logger.LogInformation($"detected out-of-band file change, rebuild CacheFumenInfo: {set.MusicId}");
                    }
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

            //logger.LogInformation($"OnUpdateInternal() done");
        }

        public string GetFumenFolderPath()
        {
            return fumenFolderPath;
        }
    }
}
