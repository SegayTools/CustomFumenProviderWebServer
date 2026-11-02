#nullable enable

using System.Net;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    /// <summary>
    /// Gmail 相关环境变量不完整，功能未启用。
    /// </summary>
    public sealed class GmailNotConfiguredException : Exception
    {
        public GmailNotConfiguredException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 没有可用的 Refresh Token，或 Google 已撤销授权，需要重新走 OAuth 授权。
    /// </summary>
    public sealed class GmailAuthorizationRequiredException : Exception
    {
        public GmailAuthorizationRequiredException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// 授权码换取 Token 失败。
    /// </summary>
    public sealed class GmailAuthorizationException : Exception
    {
        public GmailAuthorizationException(string message) : base(message)
        {
        }

        public GmailAuthorizationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Access Token 刷新失败。
    /// </summary>
    public sealed class GmailTokenException : Exception
    {
        public GmailTokenException(string message) : base(message)
        {
        }

        public GmailTokenException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Gmail API 拒绝了发信请求。
    /// </summary>
    public sealed class GmailSendException : Exception
    {
        public GmailSendException(HttpStatusCode statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}
