using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Utils;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 首次投稿时待落盘的谱面：难度下标、上传文件路径、该难度的定数。
    /// </summary>
    public sealed record FumenChartUpload(int DiffIdx, string SourceFilePath, float Level);

    /// <summary>
    /// 谱面文件（.ogkr）的归档与再生成。
    /// <para>
    /// 归档：把上传的原始谱面原样复制到 <c>fumen{musicId}/{难度}.ogkr</c>。
    /// 处理（仅当 <see cref="EnableCreatorRewriteEnvironmentVariable"/> 为 true）：把 opt 副本的
    /// CREATOR 改写为 <c>[自制谱 Lv.难度] 原谱师</c>，难度显示规则与游戏端一致
    /// （小数部分 &gt;= 70 时追加 '+'）。开关默认关闭，关闭时 opt 副本也是上传原文。
    /// </para>
    /// </summary>
    public class FumenChartService
    {
        /// <summary>CREATOR 为空或缺失时使用的默认谱师名。</summary>
        public const string DefaultCreator = "-";

        /// <summary>
        /// 是否改写 CREATOR 的环境变量名。未设置或无法解析时默认关闭。
        /// </summary>
        public const string EnableCreatorRewriteEnvironmentVariable = "EnableCreatorRewrite";

        /// <summary>游戏端 fumenLevelPlusBorder 的固定值（D:\sddt160 中硬编码为 70）。</summary>
        private const int LevelPlusBorder = 70;

        private static readonly Regex CreatorLineRegex = new(
            @"^(?<indent>[ \t]*)CREATOR(?=[ \t]|\r?$)(?<separator>[ \t]*)(?<value>[^\r\n]*)\r?$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex HeaderTagRegex = new(
            @"^(?<indent>[ \t]*)(?:VERSION|BPM_DEF|MET_DEF)(?=[ \t]|\r?$)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex SelfMadePrefixRegex = new(
            @"^\s*\[自制谱\s*Lv\.[^\]]*\]\s*",
            RegexOptions.Compiled);

        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        private readonly FumenStorageService storageService;
        private readonly bool creatorRewriteEnabled;

        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public FumenChartService(FumenStorageService storageService)
            : this(storageService, ReadCreatorRewriteEnabled())
        {
        }

        public FumenChartService(FumenStorageService storageService, bool enableCreatorRewrite)
        {
            this.storageService = storageService;
            this.creatorRewriteEnabled = enableCreatorRewrite;
        }

        /// <summary>当前是否启用 CREATOR 改写。</summary>
        public bool IsCreatorRewriteEnabled => creatorRewriteEnabled;

        private static bool ReadCreatorRewriteEnabled()
        {
            var value = Environment.GetEnvironmentVariable(EnableCreatorRewriteEnvironmentVariable);
            return bool.TryParse(value, out var enabled) && enabled;
        }

        /// <summary>
        /// 把定数转换为游戏显示的难度文本：整数部分 +（小数部分 &gt;= 70 时的 '+'）。
        /// 例：13.00 → "13"，13.69 → "13"，13.70 → "13+"。
        /// </summary>
        public static string FormatLevel(float level)
        {
            var hundredths = (int)Math.Round((decimal)level * 100m, MidpointRounding.AwayFromZero);
            if (hundredths < 0)
                hundredths = 0;

            var integerPart = hundredths / 100;
            var fractionalPart = hundredths % 100;

            return fractionalPart >= LevelPlusBorder
                ? $"{integerPart.ToString(CultureInfo.InvariantCulture)}+"
                : integerPart.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 去掉已存在的 <c>[自制谱 Lv.x]</c> 前缀并裁剪空白，保证反复处理时不会叠加前缀。
        /// </summary>
        public static string StripSelfMadePrefix(string creator)
        {
            return SelfMadePrefixRegex.Replace(creator ?? string.Empty, string.Empty).Trim();
        }

        /// <summary>
        /// 归档原始谱面，并在 opt 生成处理后的副本。
        /// </summary>
        public async Task<Result> SaveChartAsync(string sourceFilePath, string archiveFilePath, string optFilePath, float level)
        {
            try
            {
                EnsureDirectory(archiveFilePath);
                File.Copy(sourceFilePath, archiveFilePath, true);
            }
            catch (Exception e)
            {
                return new(false, $"archive fumen file failed: {e.Message}");
            }

            return await ProcessToFileAsync(archiveFilePath, optFilePath, level);
        }

        /// <summary>
        /// 用源谱面的原谱师 + 指定定数生成处理后的谱面，原子覆盖目标文件。
        /// 源文件与目标文件可以是同一个路径。
        /// </summary>
        public async Task<Result> ProcessToFileAsync(string sourceFilePath, string targetFilePath, float level)
        {
            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(sourceFilePath);
            }
            catch (Exception e)
            {
                return new(false, $"read fumen file failed: {e.Message}");
            }

            //开关关闭时保持历史行为:归档与 opt 副本都是上传原文
            if (!creatorRewriteEnabled)
                return await WriteBytesAsync(targetFilePath, bytes);

            if (!TryDecodeUtf8(bytes, out var text, out var hasBom, out var decodeError))
                return new(false, decodeError);

            if (string.IsNullOrWhiteSpace(text))
                return new(false, "fumen file is empty");

            var creatorMatch = CreatorLineRegex.Match(text);
            string originalCreator;

            if (creatorMatch.Success)
            {
                originalCreator = StripSelfMadePrefix(creatorMatch.Groups["value"].Value);
                if (originalCreator.Length == 0)
                    originalCreator = DefaultCreator;
            }
            else
            {
                if (!HeaderTagRegex.IsMatch(text))
                    return new(false, "can not locate CREATOR tag in fumen file");

                originalCreator = DefaultCreator;
            }

            var processedCreator = $"[自制谱 Lv.{FormatLevel(level)}] {originalCreator}";

            var processedText = creatorMatch.Success
                ? CreatorLineRegex.Replace(text, match => BuildCreatorLine(match, processedCreator), 1)
                : InsertCreatorLine(text, processedCreator);

            var content = new UTF8Encoding(false, true).GetBytes(processedText);
            if (hasBom)
                content = [.. new UTF8Encoding(true).GetPreamble(), .. content];

            return await WriteBytesAsync(targetFilePath, content);
        }

        private static async Task<Result> WriteBytesAsync(string targetFilePath, byte[] content)
        {
            try
            {
                EnsureDirectory(targetFilePath);
                await FileUtils.WriteAtomicAsync(targetFilePath, fs => fs.WriteAsync(content, 0, content.Length));
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"write fumen file failed: {e.Message}");
            }
        }

        /// <summary>
        /// 首次投稿时把上传的谱面写入生成的临时目录：
        /// 原始谱面归档到 archiveFolder/{难度}.ogkr，
        /// 处理后副本写到 opt/music/music{musicId:D4}/{musicId:D4}_0{diffIdx:D2}.ogkr。
        /// </summary>
        public async Task<Result> SaveDeliverChartsAsync(
            int musicId,
            string archiveFolder,
            string optMusicFolder,
            IEnumerable<FumenChartUpload> charts)
        {
            foreach (var chart in charts)
            {
                var archiveFilePath = Path.Combine(archiveFolder, FumenDifficultNames.GetArchiveFileName(chart.DiffIdx));
                var optFilePath = Path.Combine(optMusicFolder, $"{musicId:D4}_{chart.DiffIdx:D2}.ogkr");

                var result = await SaveChartAsync(chart.SourceFilePath, archiveFilePath, optFilePath, chart.Level);
                if (!result.IsSuccess)
                    return result;
            }

            return new(true);
        }

        /// <summary>
        /// 重新生成某个难度的 opt 副本。优先使用根目录归档作为源；
        /// 归档不存在（改动前上传的旧谱面）时退回使用现有 opt 文件。
        /// </summary>
        public async Task<Result> RegenerateOptChartAsync(int musicId, int diffIdx, float level)
        {
            if (!creatorRewriteEnabled)
                return new(true);

            var optFilePath = storageService.GetOptFumenPath(musicId, diffIdx);
            var archiveFilePath = storageService.GetArchivePath(musicId, diffIdx);

            string sourceFilePath;
            if (File.Exists(archiveFilePath))
                sourceFilePath = archiveFilePath;
            else if (File.Exists(optFilePath))
                sourceFilePath = optFilePath;
            else
                return new(true);

            return await ProcessToFileAsync(sourceFilePath, optFilePath, level);
        }

        private static string BuildCreatorLine(Match match, string value)
        {
            var indent = match.Groups["indent"].Value;
            var separator = match.Groups["separator"].Value;
            if (!separator.Contains('\t'))
                separator = "\t";

            // 正则的 \r?$ 会把 CRLF 中的 \r 纳入匹配，替换时必须原样补回，\n 仍留在原文里。
            var eol = match.Value.EndsWith('\r') ? "\r" : string.Empty;

            return $"{indent}CREATOR{separator}{value}{eol}";
        }

        private static string InsertCreatorLine(string text, string value)
        {
            var newline = DetectNewLine(text);
            var line = $"CREATOR\t{value}";

            var anchorMatch = HeaderTagRegex.Match(text);
            if (!anchorMatch.Success)
                return line + newline + text;

            var anchorTagEnd = anchorMatch.Index + anchorMatch.Length;
            var eolIndex = text.IndexOfAny(['\r', '\n'], anchorTagEnd);
            if (eolIndex < 0)
                return text + newline + line;

            var eolLength = text[eolIndex] == '\r' && eolIndex + 1 < text.Length && text[eolIndex + 1] == '\n' ? 2 : 1;
            return text.Insert(eolIndex + eolLength, line + newline);
        }

        private static string DetectNewLine(string text)
        {
            var lfIndex = text.IndexOf('\n');
            if (lfIndex >= 0)
                return lfIndex > 0 && text[lfIndex - 1] == '\r' ? "\r\n" : "\n";

            return text.Contains('\r') ? "\r" : "\n";
        }

        private static bool TryDecodeUtf8(byte[] bytes, out string text, out bool hasBom, out string error)
        {
            hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var offset = hasBom ? 3 : 0;

            try
            {
                text = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
                error = null;
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                error = "fumen file is not valid UTF-8 text";
                return false;
            }
        }

        private static void EnsureDirectory(string filePath)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
        }
    }
}
