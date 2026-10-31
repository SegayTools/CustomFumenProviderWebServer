using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 维护 opt 目录与 info.json / 数据库之间的一致性。
    /// <para>
    /// opt 的 Music.xml 只能由 info.json 渲染，opt 的 .ogkr 只能由归档（或既有 opt）生成；
    /// info.json 缺失时只允许从数据库重建，绝不从 opt/Music.xml 反推。
    /// </para>
    /// </summary>
    public class FumenOptConsistencyService
    {
        private static readonly Regex FumenFolderRegex = new(
            @"^fumen(?<id>\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly FumenStorageService storageService;
        private readonly FumenChartService fumenChartService;
        private readonly MusicXmlService musicXmlService;
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly FumenOperationLock fumenOperationLock;
        private readonly ILogger<FumenOptConsistencyService> logger;

        public FumenOptConsistencyService(
            FumenStorageService storageService,
            FumenChartService fumenChartService,
            MusicXmlService musicXmlService,
            IDbContextFactory<FumenDataDB> fumenDataDBFactory,
            FumenOperationLock fumenOperationLock,
            ILogger<FumenOptConsistencyService> logger)
        {
            this.storageService = storageService;
            this.fumenChartService = fumenChartService;
            this.musicXmlService = musicXmlService;
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.fumenOperationLock = fumenOperationLock;
            this.logger = logger;
        }

        /// <summary>
        /// 读取 info.json；缺失或损坏时从数据库重建并落盘。数据库也没有该曲目时返回失败。
        /// 本方法不加操作锁，调用方负责串行化。
        /// </summary>
        public async Task<Result<FumenSet>> ReadOrRebuildInfoSetAsync(int musicId)
        {
            var readResult = await storageService.ReadInfoJsonAsync(musicId);
            if (readResult.IsSuccess)
                return readResult;

            await using var db = await fumenDataDBFactory.CreateDbContextAsync();
            var set = await db.FumenSets
                .Include(x => x.FumenDifficults)
                .FirstOrDefaultAsync(x => x.MusicId == musicId);
            if (set is null)
                return new(false, $"info.json not found and database has no FumenSet: {musicId}");

            var writeResult = await storageService.WriteInfoJsonAsync(set);
            if (!writeResult.IsSuccess)
                return new(false, writeResult.Message);

            return new(true, string.Empty, set);
        }

        /// <summary>
        /// 从给定 FumenSet 渲染 opt/music/music{musicId}/Music.xml。
        /// 开关开启时所有已存在难度的定数写 0。
        /// </summary>
        public async Task<Result> WriteOptMusicXmlAsync(FumenSet set)
        {
            var musicFolder = storageService.GetOptMusicFolder(set.MusicId);
            var targetPath = Path.Combine(musicFolder, "Music.xml");
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                Directory.CreateDirectory(musicFolder);
                var generateResult = await musicXmlService.GenerateMusicXml(
                    set,
                    tempPath,
                    zeroLevels: fumenChartService.IsCreatorRewriteEnabled);
                if (!generateResult.IsSuccess)
                    return generateResult;

                File.Move(tempPath, targetPath, true);
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"write opt Music.xml failed: {e.Message}");
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 读取 info.json 后重新渲染 opt Music.xml。
        /// </summary>
        public async Task<Result> WriteOptMusicXmlFromInfoAsync(int musicId)
        {
            var infoResult = await storageService.ReadInfoJsonAsync(musicId);
            if (!infoResult.IsSuccess)
                return new(false, infoResult.Message);

            return await WriteOptMusicXmlAsync(infoResult.Data);
        }

        /// <summary>
        /// 检查并修复所有 fumen{musicId} 的 opt 状态。单首失败只记录日志，不中断其余曲目。
        /// </summary>
        public async Task<Result> EnsureAllAsync(CancellationToken cancellationToken = default)
        {
            var checkedCount = 0;
            var failedCount = 0;

            if (!Directory.Exists(storageService.RootPath))
                return new(true, "checked 0, failed 0");

            foreach (var directory in Directory.EnumerateDirectories(storageService.RootPath, "fumen*"))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var match = FumenFolderRegex.Match(Path.GetFileName(directory));
                if (!match.Success
                    || !int.TryParse(match.Groups["id"].Value, out var musicId)
                    || musicId <= 0)
                    continue;

                try
                {
                    var result = await EnsureMusicSetAsync(musicId, cancellationToken);
                    checkedCount++;
                    if (!result.IsSuccess)
                    {
                        failedCount++;
                        logger.LogWarning(
                            "opt consistency check failed for fumen{MusicId}: {Message}",
                            musicId,
                            result.Message);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    failedCount++;
                    logger.LogError(e, "opt consistency check failed for fumen{MusicId}", musicId);
                }
            }

            return new(failedCount == 0, $"checked {checkedCount}, failed {failedCount}");
        }

        /// <summary>
        /// 检查并修复单首曲子的 opt Music.xml 与 .ogkr。
        /// </summary>
        public async Task<Result> EnsureMusicSetAsync(int musicId, CancellationToken cancellationToken = default)
        {
            using var _ = await fumenOperationLock.AcquireAsync(musicId, cancellationToken);

            var ensureOptResult = storageService.EnsureOptFolder(musicId);
            if (!ensureOptResult.IsSuccess)
                return ensureOptResult;

            var infoResult = await ReadOrRebuildInfoSetAsync(musicId);
            if (!infoResult.IsSuccess)
                return new(false, infoResult.Message);

            var set = infoResult.Data;
            var changed = false;

            var xmlPath = Path.Combine(storageService.GetOptMusicFolder(musicId), "Music.xml");
            if (!IsOptMusicXmlConsistent(set, xmlPath))
            {
                var writeXmlResult = await WriteOptMusicXmlAsync(set);
                if (!writeXmlResult.IsSuccess)
                    return writeXmlResult;
                changed = true;
            }

            foreach (var diff in set.FumenDifficults.OrderBy(x => x.DifficultIndex))
            {
                var optPath = storageService.GetOptFumenPath(musicId, diff.DifficultIndex);

                if (fumenChartService.IsCreatorRewriteEnabled)
                {
                    if (!File.Exists(optPath))
                    {
                        var archivePath = storageService.GetArchivePath(musicId, diff.DifficultIndex);
                        if (!File.Exists(archivePath))
                        {
                            logger.LogWarning(
                                "opt chart missing and no archive, skip fumen{MusicId} difficulty {DifficultIndex}",
                                musicId,
                                diff.DifficultIndex);
                            continue;
                        }
                    }
                    else if (IsRewrittenChart(diff, optPath))
                    {
                        continue;
                    }
                }
                else
                {
                    if (!File.Exists(optPath) || !IsChartRewritten(optPath))
                        continue;
                }

                var regenerateResult = await fumenChartService.RegenerateOptChartAsync(
                    musicId,
                    diff.DifficultIndex,
                    diff.Level);
                if (!regenerateResult.IsSuccess)
                    return regenerateResult;

                changed = true;
            }

            var zipPath = storageService.GetZipPath(musicId);
            if (File.Exists(zipPath) && (changed || storageService.IsZipOutdated(musicId)))
            {
                var repackResult = await storageService.RepackZipAsync(musicId);
                if (!repackResult.IsSuccess)
                    return repackResult;
            }

            return new(true);
        }

        private bool IsOptMusicXmlConsistent(FumenSet set, string xmlPath)
        {
            if (!File.Exists(xmlPath))
                return false;

            XDocument musicXml;
            try
            {
                musicXml = XDocument.Load(xmlPath);
            }
            catch
            {
                return false;
            }

            var fumenElements = musicXml
                .XPathSelectElements("/MusicData/FumenData/FumenData")
                .ToArray();
            if (fumenElements.Length < 5)
                return false;

            for (var i = 0; i < 5; i++)
            {
                var fumenElement = fumenElements[i];
                var diff = set.FumenDifficults.FirstOrDefault(x => x.DifficultIndex == i);

                var expectedPath = diff is null ? string.Empty : $"{set.MusicId:D4}_0{i}.ogkr";
                var actualPath = fumenElement.XPathSelectElement("./FumenFile/path")?.Value ?? string.Empty;
                if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
                    return false;

                string expectedInteger;
                string expectedFractional;
                if (fumenChartService.IsCreatorRewriteEnabled || diff is null)
                {
                    expectedInteger = "0";
                    expectedFractional = "0";
                }
                else
                {
                    var hundredths = MusicXmlService.LevelToHundredths(diff.Level);
                    expectedInteger = (hundredths / 100).ToString();
                    expectedFractional = (hundredths % 100).ToString("D2");
                }

                var actualInteger = fumenElement.XPathSelectElement("./FumenConstIntegerPart")?.Value;
                var actualFractional = fumenElement.XPathSelectElement("./FumenConstFractionalPart")?.Value;
                if (!string.Equals(actualInteger, expectedInteger, StringComparison.Ordinal)
                    || !string.Equals(actualFractional, expectedFractional, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static bool IsRewrittenChart(FumenDifficult diff, string optPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(optPath);
                if (!FumenChartService.TryReadCreatorValue(bytes, out var creatorValue)
                    || !FumenChartService.TryParseSelfMadeCreator(creatorValue, out var levelText, out var originalCreator))
                    return false;

                var expectedCreator = diff.Creator ?? FumenChartService.DefaultCreator;
                return string.Equals(levelText, FumenChartService.FormatLevel(diff.Level), StringComparison.Ordinal)
                    && string.Equals(originalCreator, expectedCreator, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsChartRewritten(string optPath)
        {
            try
            {
                var bytes = File.ReadAllBytes(optPath);
                return FumenChartService.TryReadCreatorValue(bytes, out var creatorValue)
                    && FumenChartService.TryParseSelfMadeCreator(creatorValue, out _, out _);
            }
            catch
            {
                return false;
            }
        }
    }
}
