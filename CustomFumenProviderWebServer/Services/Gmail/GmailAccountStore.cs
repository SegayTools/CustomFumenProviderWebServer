#nullable enable

using MimeKit;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    public interface IGmailAccountStore
    {
        Task<string?> ReadSenderAddressAsync(CancellationToken cancellationToken = default);

        Task WriteSenderAddressAsync(string senderAddress, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Gmail 未配置时使用的空实现。
    /// </summary>
    public sealed class DisabledGmailAccountStore : IGmailAccountStore
    {
        public Task<string?> ReadSenderAddressAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task WriteSenderAddressAsync(string senderAddress, CancellationToken cancellationToken = default)
        {
            throw new GmailNotConfiguredException(
                "Gmail integration is not configured; the authorized account address cannot be saved.");
        }
    }

    /// <summary>
    /// 保存 OAuth 授权账户的邮箱地址。邮箱不是密钥，因此以明文保存在 GMAIL_DATA_DIR 下，
    /// 用于 GMAIL_SENDER_ADDRESS 留空时自动作为发件地址。
    /// </summary>
    public sealed class GmailAccountStore : IGmailAccountStore
    {
        private const string SenderAddressFileName = "sender_address.txt";

        private readonly string senderAddressFilePath;
        private readonly ILogger<GmailAccountStore> logger;
        private readonly SemaphoreSlim gate = new(1, 1);

        public GmailAccountStore(GmailOptions options, ILogger<GmailAccountStore> logger)
        {
            this.logger = logger;
            senderAddressFilePath = Path.Combine(options.DataDirectory, SenderAddressFileName);
        }

        public async Task<string?> ReadSenderAddressAsync(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!File.Exists(senderAddressFilePath))
                    return null;

                var address = (await File.ReadAllTextAsync(senderAddressFilePath, cancellationToken).ConfigureAwait(false)).Trim();
                if (IsValidAddress(address))
                    return address;

                logger.LogWarning("The saved Gmail sender address is invalid; ignoring it.");
                return null;
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task WriteSenderAddressAsync(string senderAddress, CancellationToken cancellationToken = default)
        {
            var address = senderAddress?.Trim() ?? string.Empty;
            if (!IsValidAddress(address))
                throw new ArgumentException("Sender address must be a valid email address.", nameof(senderAddress));

            var directory = Path.GetDirectoryName(senderAddressFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var tempFilePath = senderAddressFilePath + ".tmp";

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await File.WriteAllTextAsync(tempFilePath, address, cancellationToken).ConfigureAwait(false);

                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        File.SetUnixFileMode(tempFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }
                    catch (Exception ex) when (ex is PlatformNotSupportedException or IOException or UnauthorizedAccessException)
                    {
                        logger.LogWarning(ex, "Failed to restrict permissions on the Gmail sender address file.");
                    }
                }

                File.Move(tempFilePath, senderAddressFilePath, overwrite: true);
            }
            finally
            {
                gate.Release();
            }
        }

        private static bool IsValidAddress(string address)
        {
            return !string.IsNullOrWhiteSpace(address) &&
                   MailboxAddress.TryParse(address, out var mailbox) &&
                   mailbox.Address.Contains('@');
        }
    }
}
