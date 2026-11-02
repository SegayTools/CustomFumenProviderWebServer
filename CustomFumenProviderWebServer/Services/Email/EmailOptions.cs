#nullable enable

using MailKit.Security;
using MimeKit;

namespace CustomFumenProviderWebServer.Services.Email
{
    /// <summary>
    /// SMTP 配置。默认使用 126 邮箱的 SSL SMTP 服务。
    /// </summary>
    public sealed class EmailOptions
    {
        public const string DefaultHost = "smtp.126.com";
        public const int DefaultPort = 465;
        public const string DefaultSecurity = "ssl";
        public const string DefaultFromName = "CustomFumenChangedNotify";

        public string Host { get; init; } = DefaultHost;

        public int Port { get; init; } = DefaultPort;

        /// <summary>
        /// ssl / starttls / none / auto。
        /// 126 邮箱使用 465 + ssl。
        /// </summary>
        public string Security { get; init; } = DefaultSecurity;

        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// SMTP 授权码，不是邮箱登录密码。
        /// </summary>
        public string Password { get; init; } = string.Empty;

        public string FromAddress { get; init; } = string.Empty;

        public string FromName { get; init; } = DefaultFromName;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Host) &&
            Port is > 0 and <= 65535 &&
            IsSecurityValid &&
            !string.IsNullOrWhiteSpace(Username) &&
            !string.IsNullOrWhiteSpace(Password) &&
            IsFromAddressValid;

        public bool IsSecurityValid
        {
            get
            {
                var security = Security.Trim().ToLowerInvariant();
                return security is "ssl" or "starttls" or "none" or "auto";
            }
        }

        public bool IsFromAddressValid =>
            !string.IsNullOrWhiteSpace(FromAddress) &&
            MailboxAddress.TryParse(FromAddress, out var mailbox) &&
            mailbox.Address.Contains('@');

        public SecureSocketOptions GetSecureSocketOptions()
        {
            return Security.Trim().ToLowerInvariant() switch
            {
                "ssl" => SecureSocketOptions.SslOnConnect,
                "starttls" => SecureSocketOptions.StartTls,
                "none" => SecureSocketOptions.None,
                _ => SecureSocketOptions.Auto,
            };
        }

        public static EmailOptions FromEnvironment()
        {
            var host = ReadEnvironment("SMTP_HOST");
            if (string.IsNullOrWhiteSpace(host))
                host = DefaultHost;

            var port = DefaultPort;
            var portText = ReadEnvironment("SMTP_PORT");
            if (!string.IsNullOrWhiteSpace(portText) && !int.TryParse(portText, out port))
                port = -1;

            var security = ReadEnvironment("SMTP_SECURITY");
            if (string.IsNullOrWhiteSpace(security))
                security = DefaultSecurity;

            var username = ReadEnvironment("SMTP_USERNAME");
            var fromAddress = ReadEnvironment("SMTP_FROM_ADDRESS");
            if (string.IsNullOrWhiteSpace(fromAddress))
                fromAddress = username;

            var fromName = ReadEnvironment("SMTP_FROM_NAME");
            if (string.IsNullOrWhiteSpace(fromName))
                fromName = DefaultFromName;

            return new EmailOptions
            {
                Host = host,
                Port = port,
                Security = security,
                Username = username,
                Password = ReadEnvironment("SMTP_PASSWORD"),
                FromAddress = fromAddress,
                FromName = fromName,
            };
        }

        public IReadOnlyList<string> GetMissingSettings()
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(Host))
                missing.Add("SMTP_HOST");
            if (Port is <= 0 or > 65535)
                missing.Add("SMTP_PORT");
            if (!IsSecurityValid)
                missing.Add("SMTP_SECURITY (ssl, starttls, none, or auto)");
            if (string.IsNullOrWhiteSpace(Username))
                missing.Add("SMTP_USERNAME");
            if (string.IsNullOrWhiteSpace(Password))
                missing.Add("SMTP_PASSWORD");
            if (string.IsNullOrWhiteSpace(FromAddress))
                missing.Add("SMTP_FROM_ADDRESS");
            else if (!IsFromAddressValid)
                missing.Add("SMTP_FROM_ADDRESS (must be a valid email address)");
            return missing;
        }

        private static string ReadEnvironment(string name)
        {
            return Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;
        }
    }
}
