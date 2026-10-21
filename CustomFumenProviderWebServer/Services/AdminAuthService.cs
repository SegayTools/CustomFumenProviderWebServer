using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 管理员密码校验。与 /api/publishFumen、/api/remove 等管理端点共用同一套凭据。
    /// 管理员密码优先取自环境变量 ADMIN_PASSWORD(明文, 启动时计算哈希)；
    /// 未配置时使用内置默认哈希，保持既有部署行为不变。
    /// </summary>
    public class AdminAuthService
    {
        private const string DefaultAdminPasswordHash = "D7969812349992B265133AA1AA39BB82A7D5C9A3294C9ABDB337204C5CDE580F";

        private readonly string adminPasswordHash;

        public AdminAuthService(ILogger<AdminAuthService> logger)
        {
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(password))
            {
                adminPasswordHash = DefaultAdminPasswordHash;
                logger.LogInformation("ADMIN_PASSWORD is not set, using the built-in default admin password hash.");
            }
            else
            {
                adminPasswordHash = HashPassword(password);
                logger.LogInformation("ADMIN_PASSWORD is set, using the admin password from environment.");
            }
        }

        public bool VerifyAdminPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(HashPassword(password)),
                Encoding.ASCII.GetBytes(adminPasswordHash));
        }

        private static string HashPassword(string password)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        }
    }
}
