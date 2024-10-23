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
    }
}
