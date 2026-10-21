using System.IO.Compression;

namespace CustomFumenProviderWebServer.Services.Editor
{
    public static class EditorZip
    {
        /// <summary>
        /// 校验内容确实是 zip，避免把错误响应（例如过期的 artifact 返回的错误页）当成编辑器包发布。
        /// </summary>
        public static bool IsValid(byte[] content)
        {
            if (content is null || content.Length == 0)
                return false;

            try
            {
                using var archive = new ZipArchive(new MemoryStream(content, writable: false), ZipArchiveMode.Read);
                return true;
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }
    }
}
