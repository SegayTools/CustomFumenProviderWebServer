#nullable enable

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    public interface IGmailRefreshTokenStore
    {
        Task<string?> ReadAsync(CancellationToken cancellationToken = default);

        Task WriteAsync(string refreshToken, CancellationToken cancellationToken = default);

        Task DeleteAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Gmail 未配置时使用的空实现，避免在未启用 Gmail 的部署中加载 Data Protection 或触碰数据目录。
    /// </summary>
    public sealed class DisabledGmailRefreshTokenStore : IGmailRefreshTokenStore
    {
        public Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }

        public Task WriteAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            throw new GmailNotConfiguredException(
                "Gmail integration is not configured; the refresh token cannot be saved.");
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 使用 ASP.NET Core Data Protection 加密保存 Refresh Token。
    /// 加密密钥同样持久化在配置目录下的 keys 子目录中，
    /// 因此 GMAIL_DATA_DIR 必须挂载到持久化卷上。
    /// </summary>
    public sealed class GmailTokenStore : IGmailRefreshTokenStore
    {
        private const string TokenFileName = "refresh_token.dat";

        private readonly string tokenFilePath;
        private readonly IDataProtector protector;
        private readonly ILogger<GmailTokenStore> logger;
        private readonly SemaphoreSlim gate = new(1, 1);

        public GmailTokenStore(
            GmailOptions options,
            IDataProtectionProvider dataProtectionProvider,
            ILogger<GmailTokenStore> logger)
        {
            this.logger = logger;
            tokenFilePath = Path.Combine(options.DataDirectory, TokenFileName);
            protector = dataProtectionProvider.CreateProtector("CustomFumenProviderWebServer.Gmail.RefreshToken.v1");
        }

        public async Task<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!File.Exists(tokenFilePath))
                    return null;

                var protectedValue = await File.ReadAllTextAsync(tokenFilePath, cancellationToken).ConfigureAwait(false);
                try
                {
                    return protector.Unprotect(protectedValue);
                }
                catch (CryptographicException ex)
                {
                    logger.LogError(ex, "Failed to decrypt the saved Gmail refresh token. Re-run the Google OAuth authorization.");
                    return null;
                }
                catch (FormatException ex)
                {
                    logger.LogError(ex, "The saved Gmail refresh token is corrupted. Re-run the Google OAuth authorization.");
                    return null;
                }
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task WriteAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token must not be empty.", nameof(refreshToken));

            var directory = Path.GetDirectoryName(tokenFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var protectedValue = protector.Protect(refreshToken);
            var tempFilePath = tokenFilePath + ".tmp";

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await File.WriteAllTextAsync(tempFilePath, protectedValue, cancellationToken).ConfigureAwait(false);

                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        File.SetUnixFileMode(tempFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    }
                    catch (Exception ex) when (ex is PlatformNotSupportedException or IOException or UnauthorizedAccessException)
                    {
                        logger.LogWarning(ex, "Failed to restrict permissions on the Gmail refresh token file.");
                    }
                }

                File.Move(tempFilePath, tokenFilePath, overwrite: true);
            }
            finally
            {
                gate.Release();
            }
        }

        public async Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(tokenFilePath))
                    File.Delete(tokenFilePath);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
