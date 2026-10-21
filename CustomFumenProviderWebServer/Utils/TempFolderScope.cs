namespace CustomFumenProviderWebServer.Utils
{
    /// <summary>
    /// 创建一个临时目录，并在 Dispose 时递归删除。
    /// 用于确保方法无论从哪个分支返回都会清理临时文件。
    /// </summary>
    public sealed class TempFolderScope : IDisposable
    {
        private int disposed;

        public string Path { get; }

        public TempFolderScope()
        {
            Path = TempPathUtils.GetNewTempFolder();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
            catch (IOException)
            {
                // 清理失败不应掩盖业务的返回值；残留目录由系统临时目录回收。
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
