using System.Text.RegularExpressions;

namespace CustomFumenProviderWebServer.Services.Editor
{
    public static class EditorArtifactName
    {
        private static readonly Regex VersionRegex = new(@"OngekiFumenEditor_([\d\.]+)_", RegexOptions.Compiled);

        /// <summary>
        /// 去掉客户端可能带上的目录部分，避免下载文件名里出现路径。
        /// </summary>
        public static string SanitizeFileName(string fileName)
        {
            return Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        }

        public static bool IsZipFileName(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从 OngekiFumenEditor_&lt;version&gt;_... 中解析版本号。
        /// </summary>
        public static bool TryParse(string name, out Version version)
        {
            version = null;
            var match = VersionRegex.Match(SanitizeFileName(name));
            if (!match.Success)
                return false;

            return Version.TryParse(match.Groups[1].Value, out version);
        }
    }
}
