#nullable enable

using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.Mvc;
using MimeKit;

namespace CustomFumenProviderWebServer.Controllers
{
    /// <summary>
    /// Gmail 集成诊断接口。业务代码应直接注入 <see cref="GmailEmailSender"/>。
    /// </summary>
    [ApiController]
    [Route("api/gmail")]
    public sealed class GmailController : ControllerBase
    {
        private readonly GmailOptions gmailOptions;
        private readonly GmailEmailSender emailSender;
        private readonly AdminAuthService adminAuthService;
        private readonly ILogger<GmailController> logger;

        public GmailController(
            GmailOptions gmailOptions,
            GmailEmailSender emailSender,
            AdminAuthService adminAuthService,
            ILogger<GmailController> logger)
        {
            this.gmailOptions = gmailOptions;
            this.emailSender = emailSender;
            this.adminAuthService = adminAuthService;
            this.logger = logger;
        }

        /// <summary>
        /// 发送一封测试邮件，用于验证 Google OAuth 与 Gmail API 是否可用。
        /// </summary>
        /// <param name="to">收件人邮箱地址</param>
        /// <param name="password">管理员密码。优先使用 X-Admin-Password 请求头，避免密码出现在请求体中。</param>
        /// <param name="subject">邮件主题，可选</param>
        /// <param name="body">纯文本正文，可选</param>
        /// <returns>发送结果</returns>
        [HttpPost("sendTest")]
        public async Task<ActionResult<Result>> SendTest(
            [FromForm] string to,
            [FromForm] string? password = null,
            [FromForm] string? subject = null,
            [FromForm] string? body = null)
        {
            if (!IsAdmin(password))
                return StatusCode(StatusCodes.Status403Forbidden, new Result(false, "no permission"));

            if (!gmailOptions.IsConfigured)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new Result(false, "Gmail is not configured. Missing or invalid: " + string.Join(", ", gmailOptions.GetMissingSettings())));
            }

            if (string.IsNullOrWhiteSpace(to) || !MailboxAddress.TryParse(to, out _))
                return BadRequest(new Result(false, "invalid recipient address"));

            var message = new GmailMessage
            {
                To = new[] { to.Trim() },
                Subject = string.IsNullOrWhiteSpace(subject) ? "CustomFumenProviderWebServer test email" : subject,
                TextBody = string.IsNullOrWhiteSpace(body)
                    ? "This is a test email sent through the Gmail API. Gmail OAuth and the send pipeline are working."
                    : body,
            };

            try
            {
                var messageId = await emailSender.SendAsync(message, HttpContext.RequestAborted);
                logger.LogInformation("Gmail test email sent to {Recipient}.", to);
                return new Result(true, string.IsNullOrWhiteSpace(messageId) ? "sent" : "sent, message id: " + messageId);
            }
            catch (GmailNotConfiguredException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new Result(false, ex.Message));
            }
            catch (GmailAuthorizationRequiredException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new Result(false, ex.Message));
            }
            catch (GmailSendException ex)
            {
                logger.LogError(ex, "Gmail API rejected a test email to {Recipient}.", to);
                return StatusCode(StatusCodes.Status502BadGateway, new Result(false, ex.Message));
            }
            catch (GmailTokenException ex)
            {
                logger.LogError(ex, "Gmail token refresh failed while sending a test email.");
                return StatusCode(StatusCodes.Status502BadGateway, new Result(false, ex.Message));
            }
        }

        private bool IsAdmin(string? password)
        {
            var headerPassword = Request.Headers["X-Admin-Password"].FirstOrDefault();
            var provided = string.IsNullOrWhiteSpace(headerPassword) ? password : headerPassword;
            return adminAuthService.VerifyAdminPassword(provided ?? string.Empty);
        }
    }
}
