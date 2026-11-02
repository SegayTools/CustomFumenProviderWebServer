#nullable enable

using MimeKit;

namespace CustomFumenProviderWebServer.Services.Email
{
    /// <summary>
    /// 定时活动报表配置。发送目标通过 REPORT_RECIPIENTS 配置，可多个，逗号分隔。
    /// </summary>
    public sealed class ActivityReportOptions
    {
        public const string DefaultRecipient = "mikirasora0409@126.com";
        public const int DefaultIntervalHours = 6;
        public const int DefaultUtcOffsetHours = 8;

        public bool Enabled { get; init; } = true;

        public IReadOnlyList<string> Recipients { get; init; } = new[] { DefaultRecipient };

        public int IntervalHours { get; init; } = DefaultIntervalHours;

        /// <summary>
        /// 报表展示使用的 UTC 偏移。默认 +8（中国标准时间）。
        /// </summary>
        public int UtcOffsetHours { get; init; } = DefaultUtcOffsetHours;

        public int MaxRows { get; init; } = 500;

        public string SiteUrl { get; init; } = "https://fumen.nageki-net.com";

        public bool IsConfigured =>
            Enabled &&
            Recipients.Count > 0 &&
            IntervalHours is >= 1 and <= 168 &&
            UtcOffsetHours is >= -12 and <= 14 &&
            MaxRows > 0;

        public TimeSpan Interval => TimeSpan.FromHours(IntervalHours);

        public TimeSpan Offset => TimeSpan.FromHours(UtcOffsetHours);

        public static ActivityReportOptions FromEnvironment()
        {
            var enabledText = ReadEnvironment("REPORT_ENABLED");
            var enabled = string.IsNullOrWhiteSpace(enabledText) || IsTrue(enabledText);

            var recipientsText = ReadEnvironment("REPORT_RECIPIENTS");
            var recipients = ParseRecipients(recipientsText);
            if (recipients.Count == 0 && string.IsNullOrWhiteSpace(recipientsText))
                recipients = new[] { DefaultRecipient };

            var intervalHours = ParseInt("REPORT_INTERVAL_HOURS", DefaultIntervalHours);
            var utcOffsetHours = ParseInt("REPORT_UTC_OFFSET_HOURS", DefaultUtcOffsetHours);
            var maxRows = ParseInt("REPORT_MAX_ROWS", 500);

            var siteUrl = ReadEnvironment("REPORT_SITE_URL");
            if (string.IsNullOrWhiteSpace(siteUrl))
                siteUrl = "https://fumen.nageki-net.com";

            return new ActivityReportOptions
            {
                Enabled = enabled,
                Recipients = recipients,
                IntervalHours = intervalHours,
                UtcOffsetHours = utcOffsetHours,
                MaxRows = maxRows,
                SiteUrl = siteUrl,
            };
        }

        private static IReadOnlyList<string> ParseRecipients(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            return text
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(address => MailboxAddress.TryParse(address, out var mailbox) && mailbox.Address.Contains('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static int ParseInt(string name, int defaultValue)
        {
            var text = ReadEnvironment(name);
            if (string.IsNullOrWhiteSpace(text))
                return defaultValue;

            return int.TryParse(text, out var value) ? value : -1;
        }

        private static bool IsTrue(string text)
        {
            return text.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
        }

        private static string ReadEnvironment(string name)
        {
            return Environment.GetEnvironmentVariable(name)?.Trim() ?? string.Empty;
        }
    }
}
