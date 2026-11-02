#nullable enable

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace CustomFumenProviderWebServer.Services.Gmail
{
    /// <summary>
    /// 保存一次性 OAuth state 并限制有效期，防止 CSRF 与授权请求重放。
    /// 单实例部署下使用内存即可；多实例部署需要改为共享存储。
    /// </summary>
    public sealed class GoogleOAuthStateStore
    {
        private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private readonly ConcurrentDictionary<string, DateTimeOffset> states = new(StringComparer.Ordinal);
        private readonly TimeProvider timeProvider;

        public GoogleOAuthStateStore(TimeProvider timeProvider)
        {
            this.timeProvider = timeProvider;
        }

        public string Create()
        {
            RemoveExpired();

            var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
            states[state] = timeProvider.GetUtcNow().Add(Lifetime);
            return state;
        }

        public bool TryConsume(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return false;
            if (!states.TryRemove(state, out var expiresAt))
                return false;

            return timeProvider.GetUtcNow() <= expiresAt;
        }

        private void RemoveExpired()
        {
            var now = timeProvider.GetUtcNow();
            foreach (var pair in states)
            {
                if (pair.Value < now)
                    states.TryRemove(pair.Key, out _);
            }
        }
    }
}
