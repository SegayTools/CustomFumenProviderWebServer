namespace CustomFumenProviderWebServer.Utils
{
    public static class AudioFileTypes
    {
        public static bool IsZip(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 在目录中按扩展名查找文件，忽略大小写（Linux 上 .ACB/.AWB 很常见）。
        /// </summary>
        public static string FindFileWithExtension(string folder, string extension)
        {
            return Directory.GetFiles(folder)
                .FirstOrDefault(file => string.Equals(Path.GetExtension(file), extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
