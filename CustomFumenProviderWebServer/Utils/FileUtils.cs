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
    }
}
