#nullable enable

using MimeKit;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    /// <summary>
    /// Gmail API / Google OAuth 配置。
    /// 所有参数均通过环境变量提供，便于在 docker compose 中注入而无需重建镜像。
    /// </summary>
    public sealed class GmailOptions
    {
        public const string SendScope = "https://www.googleapis.com/auth/gmail.send";
        public const string OAuthScope = "openid email " + SendScope;
        public const string HttpClientName = "Gmail";

        private const string DefaultPublicAppName = "CustomFumenChangedNotify";
        private const string DefaultPublicContactEmail = "mikirasora0409@gmail.com";

        public string ClientId { get; init; } = string.Empty;

        public string ClientSecret { get; init; } = string.Empty;

        /// <summary>
        /// 必须与 Google Cloud Console 中登记的重定向 URI 完全一致，
        /// 例如 https://fumen.nageki-net.com/oauth/google/callback。
        /// </summary>
        public string RedirectUri { get; init; } = string.Empty;

        /// <summary>
        /// 可选的发件地址。留空时从 OAuth 授权返回的 ID Token 中自动读取授权账户邮箱。
        /// 如果填写，必须是完成授权的 Gmail 账户本身或其已验证别名。
        /// </summary>
        public string SenderAddress { get; init; } = string.Empty;

        /// <summary>
        /// Refresh Token 与 Data Protection 密钥的持久化目录。
        /// 容器部署时应挂载到卷上，否则容器重建后需要重新授权。
        /// </summary>
        public string DataDirectory { get; init; } = string.Empty;

        /// <summary>
        /// 可选的初始 Refresh Token。没有已保存的 Token 时作为回退值使用。
        /// 正式环境更推荐通过 /oauth/google/start 完成一次授权后由服务自行保存。
        /// </summary>
        public string? RefreshToken { get; init; }

        /// <summary>
        /// 公开审核页面显示的应用名称，必须与 Google OAuth 同意屏幕中的应用名称完全一致。
        /// </summary>
        public string PublicAppName { get; init; } = DefaultPublicAppName;

        /// <summary>
        /// 公开审核页面显示的支持邮箱。
        /// </summary>
        public string PublicContactEmail { get; init; } = DefaultPublicContactEmail;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) &&
            !string.IsNullOrWhiteSpace(ClientSecret) &&
            IsRedirectUriValid &&
            (string.IsNullOrWhiteSpace(SenderAddress) || IsSenderAddressValid);

        public bool RedirectUriConfigured => !string.IsNullOrWhiteSpace(RedirectUri);

        public bool IsRedirectUriValid =>
            Uri.TryCreate(RedirectUri, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback) &&
            uri.AbsolutePath == "/oauth/google/callback" &&
            string.IsNullOrEmpty(uri.Query) &&
            string.IsNullOrEmpty(uri.Fragment);

        public bool IsSenderAddressValid =>
            !string.IsNullOrWhiteSpace(SenderAddress) &&
            MailboxAddress.TryParse(SenderAddress, out var mailbox) &&
            mailbox.Address.Contains('@');

        public static GmailOptions FromEnvironment()
        {
            var dataDirectory = ReadEnvironment("GMAIL_DATA_DIR");
            if (string.IsNullOrWhiteSpace(dataDirectory))
                dataDirectory = Path.Combine(AppContext.BaseDirectory, "data", "gmail");

            var publicAppName = ReadEnvironment("GMAIL_PUBLIC_APP_NAME");
            if (string.IsNullOrWhiteSpace(publicAppName))
                publicAppName = DefaultPublicAppName;

            var publicContactEmail = ReadEnvironment("GMAIL_PUBLIC_CONTACT_EMAIL");
            if (string.IsNullOrWhiteSpace(publicContactEmail))
                publicContactEmail = DefaultPublicContactEmail;

            return new GmailOptions
            {
                ClientId = ReadEnvironment("GMAIL_CLIENT_ID"),
                ClientSecret = ReadEnvironment("GMAIL_CLIENT_SECRET"),
                RedirectUri = ReadEnvironment("GMAIL_REDIRECT_URI"),
                SenderAddress = ReadEnvironment("GMAIL_SENDER_ADDRESS"),
                DataDirectory = dataDirectory,
                RefreshToken = ReadEnvironment("GMAIL_REFRESH_TOKEN"),
                PublicAppName = publicAppName,
                PublicContactEmail = publicContactEmail,
            };
        }

        /// <summary>
        /// 返回仍缺失的必填环境变量名称，用于启动日志。
        /// </summary>
        public IReadOnlyList<string> GetMissingSettings()
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(ClientId))
                missing.Add("GMAIL_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(ClientSecret))
                missing.Add("GMAIL_CLIENT_SECRET");
            if (!RedirectUriConfigured)
                missing.Add("GMAIL_REDIRECT_URI");
            else if (!IsRedirectUriValid)
                missing.Add("GMAIL_REDIRECT_URI (must be an absolute HTTPS URI ending with /oauth/google/callback; HTTP is allowed on localhost)");
            if (!string.IsNullOrWhiteSpace(SenderAddress) && !IsSenderAddressValid)
                missing.Add("GMAIL_SENDER_ADDRESS (must be a valid email address)");
            return missing;
        }

        private static string ReadEnvironment(string name)
        {
            return Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;
        }
    }
}
