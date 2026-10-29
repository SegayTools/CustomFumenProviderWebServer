using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// Music.xml 的原始归档。
    /// <para>
    /// 用户提供的 Music.xml 原样保存为 <c>fumen{musicId}/music.xml</c>；
    /// opt 下的 <c>Music.xml</c> 仍由 <see cref="MusicXmlService.GenerateMusicXml"/> 生成
    /// （Music.xml 的后处理规则尚未定义，当前以现有生成结果作为处理产物）。
    /// </para>
    /// </summary>
    public class FumenXmlService
    {
        /// <summary>归档文件固定名（小写），与游戏读取的 Music.xml 区分。</summary>
        public const string ArchiveFileName = "music.xml";

        private readonly FumenStorageService storageService;

        public FumenXmlService(FumenStorageService storageService)
        {
            this.storageService = storageService;
        }

        /// <summary>把提供的 Music.xml 原样归档到 fumen{musicId}/music.xml。</summary>
        public Task<Result> ArchiveProvidedXmlAsync(int musicId, string sourceXmlFilePath)
        {
            return CopyAtomicAsync(sourceXmlFilePath, storageService.GetArchiveXmlPath(musicId));
        }

        /// <summary>把提供的 Music.xml 原样归档到指定目录（投稿时是生成目录的根）。</summary>
        public Task<Result> ArchiveProvidedXmlAsync(string archiveFolder, string sourceXmlFilePath)
        {
            return CopyAtomicAsync(sourceXmlFilePath, Path.Combine(archiveFolder, ArchiveFileName));
        }

        private static async Task<Result> CopyAtomicAsync(string sourceXmlFilePath, string targetXmlFilePath)
        {
            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(sourceXmlFilePath);
            }
            catch (Exception e)
            {
                return new(false, $"read Music.xml failed: {e.Message}");
            }

            try
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(targetXmlFilePath));
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                await FileUtils.WriteAtomicAsync(targetXmlFilePath, fs => fs.WriteAsync(bytes, 0, bytes.Length));
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"write music.xml failed: {e.Message}");
            }
        }
    }
}
