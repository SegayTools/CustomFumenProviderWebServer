using Microsoft.AspNetCore.Http;

namespace CustomFumenProviderWebServer.Utils
{
    public static class FormFileUtils
    {
        /// <summary>
        /// 把上传文件保存到指定路径，并确保上传流被释放。
        /// </summary>
        public static async Task SaveAsync(IFormFile file, string saveFilePath)
        {
            if (file is null)
                return;

            await using var formStream = file.OpenReadStream();
            await using var fs = new FileStream(saveFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await formStream.CopyToAsync(fs);
        }
    }
}
