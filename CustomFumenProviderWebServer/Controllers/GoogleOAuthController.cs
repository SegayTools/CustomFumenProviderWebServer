#nullable enable

using System.Text.Encodings.Web;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.Mvc;

namespace CustomFumenProviderWebServer.Controllers
{
    /// <summary>
    /// Google OAuth 2.0 授权入口与回调。首次部署时由管理员访问一次即可。
    /// </summary>
    [ApiController]
    public sealed class GoogleOAuthController : ControllerBase
    {
        private readonly GmailOptions gmailOptions;
        private readonly GoogleOAuthStateStore stateStore;
        private readonly GmailAuthorizationService authorizationService;
        private readonly IGmailAccessTokenProvider accessTokenProvider;
        private readonly IGmailAccountStore accountStore;
        private readonly AdminAuthService adminAuthService;
        private readonly ILogger<GoogleOAuthController> logger;

        public GoogleOAuthController(
            GmailOptions gmailOptions,
            GoogleOAuthStateStore stateStore,
            GmailAuthorizationService authorizationService,
            IGmailAccessTokenProvider accessTokenProvider,
            IGmailAccountStore accountStore,
            AdminAuthService adminAuthService,
            ILogger<GoogleOAuthController> logger)
        {
            this.gmailOptions = gmailOptions;
            this.stateStore = stateStore;
            this.authorizationService = authorizationService;
            this.accessTokenProvider = accessTokenProvider;
            this.accountStore = accountStore;
            this.adminAuthService = adminAuthService;
            this.logger = logger;
        }

        /// <summary>
        /// 开始 Google OAuth 授权。需要管理员密码，可通过 X-Admin-Password 请求头或 password 查询参数提供。
        /// </summary>
        /// <param name="password">管理员密码。优先使用 X-Admin-Password 请求头，避免密码出现在访问日志中。</param>
        /// <returns>重定向到 Google 授权页面</returns>
        [HttpGet("oauth/google/start")]
        public IActionResult Start([FromQuery] string? password = null)
        {
            Response.Headers.CacheControl = "no-store";

            if (!IsAdmin(password))
                return StatusCode(StatusCodes.Status403Forbidden, new Result(false, "no permission"));

            if (!gmailOptions.IsConfigured)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new Result(false, NotConfiguredMessage()));
            }

            var state = stateStore.Create();
            var authorizationUri = authorizationService.CreateAuthorizationUri(state);
            return Redirect(authorizationUri.AbsoluteUri);
        }

        /// <summary>
        /// Google OAuth 回调。校验 state 后把 authorization code 换成 Refresh Token 并加密保存。
        /// </summary>
        /// <param name="code">Google 返回的 authorization code</param>
        /// <param name="state">授权请求中生成的一次性 state</param>
        /// <param name="error">用户拒绝授权时 Google 返回的错误代码</param>
        /// <returns>供浏览器显示的授权结果页面</returns>
        [HttpGet("oauth/google/callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code = null,
            [FromQuery] string? state = null,
            [FromQuery] string? error = null)
        {
            Response.Headers.CacheControl = "no-store";

            if (!gmailOptions.IsConfigured)
                return HtmlPage(StatusCodes.Status503ServiceUnavailable, "Gmail 未配置", NotConfiguredMessage());

            if (!string.IsNullOrWhiteSpace(error))
            {
                stateStore.TryConsume(state);
                return HtmlPage(StatusCodes.Status400BadRequest, "授权未完成", "Google 返回错误：" + error);
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                stateStore.TryConsume(state);
                return HtmlPage(StatusCodes.Status400BadRequest, "授权失败", "回调中没有 authorization code。");
            }

            if (!stateStore.TryConsume(state))
            {
                return HtmlPage(
                    StatusCodes.Status400BadRequest,
                    "授权失败",
                    "state 无效、已过期或已被使用。请重新访问 /oauth/google/start。");
            }

            try
            {
                await authorizationService.ExchangeCodeAsync(code, HttpContext.RequestAborted);
                accessTokenProvider.Invalidate();
                return HtmlPage(StatusCodes.Status200OK, "授权成功", "Gmail Refresh Token 已加密保存，可以关闭此页面。");
            }
            catch (GmailAuthorizationException ex)
            {
                logger.LogError(ex, "Google OAuth callback failed.");
                return HtmlPage(StatusCodes.Status502BadGateway, "授权失败", ex.Message);
            }
            catch (GmailNotConfiguredException ex)
            {
                return HtmlPage(StatusCodes.Status503ServiceUnavailable, "Gmail 未配置", ex.Message);
            }
        }

        /// <summary>
        /// 查询 Gmail 集成配置与授权状态。需要管理员密码。
        /// </summary>
        /// <param name="password">管理员密码。优先使用 X-Admin-Password 请求头。</param>
        /// <returns>是否已配置以及是否已保存 Refresh Token</returns>
        [HttpGet("oauth/google/status")]
        public async Task<ActionResult<GmailAuthorizationStatus>> Status([FromQuery] string? password = null)
        {
            if (!IsAdmin(password))
                return StatusCode(StatusCodes.Status403Forbidden, new Result(false, "no permission"));

            var authorized = gmailOptions.IsConfigured &&
                             await accessTokenProvider.HasRefreshTokenAsync(HttpContext.RequestAborted);

            var senderAddress = gmailOptions.SenderAddress;
            if (string.IsNullOrWhiteSpace(senderAddress) && gmailOptions.IsConfigured)
            {
                senderAddress = await accountStore.ReadSenderAddressAsync(HttpContext.RequestAborted) ?? string.Empty;
            }

            return new GmailAuthorizationStatus(
                gmailOptions.IsConfigured,
                authorized,
                senderAddress,
                gmailOptions.RedirectUri);
        }

        private bool IsAdmin(string? password)
        {
            var headerPassword = Request.Headers["X-Admin-Password"].FirstOrDefault();
            var provided = string.IsNullOrWhiteSpace(headerPassword) ? password : headerPassword;
            return adminAuthService.VerifyAdminPassword(provided ?? string.Empty);
        }

        private string NotConfiguredMessage()
        {
            var missing = gmailOptions.GetMissingSettings();
            return missing.Count == 0
                ? "Gmail 集成未启用。"
                : "Gmail 集成未启用，缺少或无效的环境变量：" + string.Join(", ", missing);
        }

        private static ContentResult HtmlPage(int statusCode, string title, string message)
        {
            var encodedTitle = HtmlEncoder.Default.Encode(title);
            var encodedMessage = HtmlEncoder.Default.Encode(message);

            return new ContentResult
            {
                StatusCode = statusCode,
                ContentType = "text/html; charset=utf-8",
                Content = $"""
                    <!doctype html>
                    <html lang="zh-CN">
                    <head>
                      <meta charset="utf-8">
                      <meta name="viewport" content="width=device-width, initial-scale=1">
                      <title>{encodedTitle}</title>
                    </head>
                    <body>
                      <h1>{encodedTitle}</h1>
                      <p>{encodedMessage}</p>
                    </body>
                    </html>
                    """,
            };
        }
    }
}
