#nullable enable

using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Email;
using Microsoft.AspNetCore.Mvc;
using MimeKit;

namespace CustomFumenProviderWebServer.Controllers
{
    /// <summary>
    /// 邮件发送诊断接口。业务代码应直接注入 <see cref="SmtpEmailSender"/>。
    /// </summary>
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Route("api/email")]
    public sealed class EmailController : ControllerBase
    {
        private readonly EmailOptions emailOptions;
        private readonly SmtpEmailSender emailSender;
        private readonly AdminAuthService adminAuthService;
        private readonly ILogger<EmailController> logger;

        public EmailController(
            EmailOptions emailOptions,
            SmtpEmailSender emailSender,
            AdminAuthService adminAuthService,
            ILogger<EmailController> logger)
        {
            this.emailOptions = emailOptions;
            this.emailSender = emailSender;
            this.adminAuthService = adminAuthService;
            this.logger = logger;
        }

        /// <summary>
        /// 发送一封测试邮件，用于验证 SMTP 配置。
        /// </summary>
        /// <param name="to">收件人邮箱地址</param>
        /// <param name="password">管理员密码。优先使用 X-Admin-Password 请求头。</param>
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

            if (!emailOptions.IsConfigured)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new Result(false, "SMTP email is not configured. Missing or invalid: " + string.Join(", ", emailOptions.GetMissingSettings())));
            }

            if (string.IsNullOrWhiteSpace(to) || !MailboxAddress.TryParse(to, out _))
                return BadRequest(new Result(false, "invalid recipient address"));

            var message = new EmailMessage
            {
                To = new[] { to.Trim() },
                Subject = string.IsNullOrWhiteSpace(subject) ? "CustomFumenProviderWebServer test email" : subject,
                TextBody = string.IsNullOrWhiteSpace(body)
                    ? "This is a test email sent through the configured SMTP server."
                    : body,
            };

            try
            {
                var messageId = await emailSender.SendAsync(message, HttpContext.RequestAborted);
                logger.LogInformation("SMTP test email sent to {Recipient}.", to);
                return new Result(true, "sent, message id: " + messageId);
            }
            catch (EmailNotConfiguredException ex)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new Result(false, ex.Message));
            }
            catch (EmailSendException ex)
            {
                logger.LogError(ex, "SMTP send failed for a test email to {Recipient}.", to);
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
