using System.Text;

namespace CustomFumenProviderWebServer.Utils
{
    public static class FileUtils
    {
        public static Task<bool> CheckFileSignature(string filePath, string magic, Encoding encoding = default) => CheckFileSignature(filePath, (encoding ?? Encoding.ASCII).GetBytes(magic));
        public static async Task<bool> CheckFileSignature(string filePath, byte[] magic)
        {
            using var fs = File.OpenRead(filePath);
            byte[] signature = new byte[magic.Length];

            int i = 0;
            while (true)
            {
                var r = await fs.ReadAsync(signature, i, magic.Length - i, default);
                i += r;

                if (i >= magic.Length)
                    return signature.SequenceEqual(magic);
                if (r == 0)
                    break;
            }

            return false;
        }

        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
                Directory.CreateDirectory(destinationDir);

            foreach (string filePath in Directory.GetFiles(sourceDir))
                File.Copy(filePath, Path.Combine(destinationDir, Path.GetFileName(filePath)), true);

            foreach (string subDir in Directory.GetDirectories(sourceDir))
                CopyDirectory(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
        }

        /// <summary>
        /// 先写同目录下的临时文件,再原子替换目标文件。
        /// 直接用 File.OpenWrite 覆盖已存在的文件不会截断,内容变短时会残留旧数据的尾部。
        /// </summary>
        public static async Task WriteAtomicAsync(string filePath, Func<Stream, Task> writeContent)
        {
            var tempFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? ".", $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                await using (var fs = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await writeContent(fs);

                File.Move(tempFilePath, filePath, true);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }
    }
}
