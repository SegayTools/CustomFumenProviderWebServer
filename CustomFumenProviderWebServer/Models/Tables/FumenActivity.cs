#nullable enable

namespace CustomFumenProviderWebServer.Models.Tables
{
    public enum FumenActivityType
    {
        Submitted,
        Modified,
        Deleted,
    }

    /// <summary>
    /// 谱面投稿/修改/删除审计记录。由后台报表服务按时间窗口汇总。
    /// </summary>
    public class FumenActivity
    {
        public long Id { get; set; }

        public int MusicId { get; set; }

        public string? Title { get; set; }

        public string? Artist { get; set; }

        public string? OwnerContact { get; set; }

        public string? OwnerImContact { get; set; }

        public FumenActivityType Type { get; set; }

        public string? Detail { get; set; }

        /// <summary>
        /// UTC 时间。MySQL 不保存 offset，读取后按 UTC 解释。
        /// </summary>
        public DateTime OccurredAtUtc { get; set; }
    }

    /// <summary>
    /// 已成功发送的活动报表。用于确定下一次报表的时间窗口，重启后不会丢失进度。
    /// </summary>
    public class FumenActivityReport
    {
        public long Id { get; set; }

        public DateTime PeriodStartUtc { get; set; }

        public DateTime PeriodEndUtc { get; set; }

        public DateTime SentAtUtc { get; set; }

        public int EventCount { get; set; }

        public string? Recipients { get; set; }
    }
}
