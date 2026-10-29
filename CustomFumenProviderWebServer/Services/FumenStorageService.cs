using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 谱面存储目录的读写操作。路径规则集中在这里，便于复用与测试。
    /// </summary>
    public class FumenStorageService
    {
        private static readonly JsonSerializerOptions jsonSerializerOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly string fumenFolderPath;

        [ActivatorUtilitiesConstructor]
        public FumenStorageService() : this(Environment.GetEnvironmentVariable("FumenDirectory"))
        {
        }

        public FumenStorageService(string fumenFolderPath)
        {
            if (string.IsNullOrWhiteSpace(fumenFolderPath))
                throw new ArgumentException("FumenDirectory is empty.", nameof(fumenFolderPath));

            this.fumenFolderPath = Path.GetFullPath(fumenFolderPath);
        }

        public string GetStoragePath(int musicId)
        {
            return Path.Combine(fumenFolderPath, $"fumen{musicId:D4}");
        }

        public string GetOptPath(int musicId)
        {
            return Path.Combine(GetStoragePath(musicId), "opt");
        }

        /// <summary>
        /// 游戏读取的谱面目录：opt/music/music{musicId:D4}
        /// </summary>
        public string GetOptMusicFolder(int musicId)
        {
            return Path.Combine(GetOptPath(musicId), "music", $"music{musicId:D4}");
        }

        /// <summary>
        /// 游戏读取的处理后谱面：opt/music/music{musicId:D4}/{musicId:D4}_0{diffIdx:D2}.ogkr
        /// </summary>
        public string GetOptFumenPath(int musicId, int diffIdx)
        {
            return Path.Combine(GetOptMusicFolder(musicId), $"{musicId:D4}_{diffIdx:D2}.ogkr");
        }

        /// <summary>
        /// 上传原始谱面的归档路径：fumen{musicId:D4}/{basic|advanced|expert|master|lunatic}.ogkr
        /// </summary>
        public string GetArchivePath(int musicId, int diffIdx)
        {
            return Path.Combine(GetStoragePath(musicId), FumenDifficultNames.GetArchiveFileName(diffIdx));
        }

        public string GetZipPath(int musicId)
        {
            return Path.Combine(GetStoragePath(musicId), $"fumen{musicId:D4}.zip");
        }

        /// <summary>
        /// 将内容写入 fumen{musicId:D4} 内的任意相对路径（新建或覆盖）。
        /// 相对路径逃出存储目录时返回失败；成功后强制把文件最后修改时间设为当前时间。
        /// </summary>
        public async Task<Result> WriteRelativeFileAsync(int musicId, string relativePath, Stream content)
        {
            if (musicId <= 0)
                return new(false, $"musicId must be positive, got {musicId}");
            if (string.IsNullOrWhiteSpace(relativePath))
                return new(false, "relativePath is empty");
            if (content is null)
                return new(false, "content is null");

            var storagePath = GetStoragePath(musicId);

            try
            {
                var normalizedRelativePath = relativePath.Trim().Replace('\\', '/');
                if (Path.IsPathRooted(normalizedRelativePath))
                    return new(false, $"relativePath must be relative: {relativePath}");

                var fullPath = Path.GetFullPath(Path.Combine(storagePath, normalizedRelativePath));
                var escapedRelativePath = Path.GetRelativePath(storagePath, fullPath);
                if (escapedRelativePath == "."
                    || escapedRelativePath == ".."
                    || escapedRelativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    || escapedRelativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                    || Path.IsPathRooted(escapedRelativePath))
                    return new(false, $"relativePath escapes fumen storage: {relativePath}");

                if (Directory.Exists(fullPath))
                    return new(false, $"target path is a directory: {relativePath}");

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await FileUtils.WriteAtomicAsync(fullPath, async fs => await content.CopyToAsync(fs));
                File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow);
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"write file failed: {e.Message}");
            }
        }

        /// <summary>
        /// 原子写入 info.json。内容变短时不会残留旧数据的尾部。
        /// </summary>
        public async Task<Result> WriteInfoJsonAsync(FumenSet set)
        {
            var storagePath = GetStoragePath(set.MusicId);
            var jsonFile = Path.Combine(storagePath, "info.json");

            try
            {
                Directory.CreateDirectory(storagePath);
                await FileUtils.WriteAtomicAsync(jsonFile, async fs =>
                    await JsonSerializer.SerializeAsync(fs, set, jsonSerializerOptions));
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"write info.json failed: {e.Message}");
            }
        }

        /// <summary>
        /// 删除 fumen{musicId} 整个目录（zip、opt、info.json、封面等）。
        /// </summary>
        public Result DeleteStorage(int musicId)
        {
            var storagePath = GetStoragePath(musicId);

            try
            {
                if (Directory.Exists(storagePath))
                    Directory.Delete(storagePath, true);

                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"delete fumen storage failed: {e.Message}");
            }
        }

        /// <summary>
        /// 确保 opt 目录存在；缺失时从 fumen{musicId}.zip 解包。
        /// zip 也不存在时返回失败，而不是抛出异常。
        /// </summary>
        public Result EnsureOptFolder(int musicId)
        {
            var optPath = GetOptPath(musicId);
            if (Directory.Exists(optPath))
                return new(true);

            var zipPath = GetZipPath(musicId);
            if (!File.Exists(zipPath))
                return new(false, $"fumen{musicId:D4}/opt and fumen{musicId:D4}.zip do not exist");

            try
            {
                Directory.CreateDirectory(optPath);
                ZipFile.ExtractToDirectory(zipPath, optPath, true);
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"extract fumen zip failed: {e.Message}");
            }
        }
    }
}
