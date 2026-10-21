using Microsoft.AspNetCore.Http.Features;

namespace CustomFumenProviderWebServer.Utils
{
    public static class FumenUploadLimits
    {
        /// <summary>
        /// 上传接口允许的最大请求体大小(300MB)。同时用于 Kestrel 的 RequestSizeLimit
        /// 和 multipart 表单解析上限，避免后者仍停留在默认的 128MB。
        /// </summary>
        public const long MaxRequestBodySize = 300_000_000;

        public static void ConfigureFormOptions(FormOptions options)
        {
            options.MultipartBodyLengthLimit = MaxRequestBodySize;
        }
    }
}
