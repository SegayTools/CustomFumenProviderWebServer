namespace CustomFumenProviderWebServer.Utils
{
    /// <summary>
    /// 创建用于解压嵌入资源的临时目录。
    /// 不使用 Path.GetTempFileName()，避免每创建一个目录就留下一个 0 字节临时文件。
    /// </summary>
    public static class TempDirectoryFactory
    {
        public static string Create(string prefix)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{prefix}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
