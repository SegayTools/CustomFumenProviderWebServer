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

        public string GetZipPath(int musicId)
        {
            return Path.Combine(GetStoragePath(musicId), $"fumen{musicId:D4}.zip");
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
