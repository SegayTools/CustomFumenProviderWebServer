#nullable enable

using System.Text.Encodings.Web;
using CustomFumenProviderWebServer.Models.Tables;

namespace CustomFumenProviderWebServer.Services.Email
{
    /// <summary>
    /// 生成活动报表邮件的纯文本与 HTML 内容。
    /// </summary>
    public static class ActivityReportBuilder
    {
        public static EmailMessage Build(
            ActivityReportOptions options,
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            IReadOnlyList<FumenActivity> activities)
        {
            var offset = options.Offset;
            var offsetLabel = FormatOffset(offset);
            var startLocal = ToLocal(periodStartUtc, offset);
            var endLocal = ToLocal(periodEndUtc, offset);

            var submitted = activities.Count(x => x.Type == FumenActivityType.Submitted);
            var modified = activities.Count(x => x.Type == FumenActivityType.Modified);
            var deleted = activities.Count(x => x.Type == FumenActivityType.Deleted);

            var subject =
                $"Fumen activity report {startLocal:yyyy-MM-dd HH:mm} - {endLocal:HH:mm} {offsetLabel} | " +
                $"{submitted} new, {modified} modified, {deleted} deleted";

            return new EmailMessage
            {
                To = options.Recipients,
                Subject = subject,
                TextBody = BuildText(options, startLocal, endLocal, offsetLabel, activities, submitted, modified, deleted),
                HtmlBody = BuildHtml(options, startLocal, endLocal, offsetLabel, activities, submitted, modified, deleted),
            };
        }

        private static string BuildText(
            ActivityReportOptions options,
            DateTimeOffset startLocal,
            DateTimeOffset endLocal,
            string offsetLabel,
            IReadOnlyList<FumenActivity> activities,
            int submitted,
            int modified,
            int deleted)
        {
            var lines = new List<string>
            {
                "Fumen activity report",
                $"Period: {startLocal:yyyy-MM-dd HH:mm} - {endLocal:yyyy-MM-dd HH:mm} ({offsetLabel})",
                "",
                "Summary:",
                $"  New submissions:   {submitted}",
                $"  File modifications: {modified}",
                $"  Deletions:         {deleted}",
                "",
            };

            if (activities.Count == 0)
            {
                lines.Add("No submissions, file modifications, or deletions were detected in this period.");
            }
            else
            {
                lines.Add("Details:");
                foreach (var activity in activities.Take(options.MaxRows))
                {
                    var time = ToLocal(activity.OccurredAtUtc, options.Offset).ToString("yyyy-MM-dd HH:mm:ss");
                    var owner = FormatOwner(activity);
                    lines.Add(
                        $"[{time}] {TypeLabel(activity.Type)} | musicId={activity.MusicId} | " +
                        $"title={activity.Title ?? "-"} | artist={activity.Artist ?? "-"} | " +
                        $"owner={owner} | detail={activity.Detail ?? "-"}");
                }

                if (activities.Count > options.MaxRows)
                    lines.Add($"... and {activities.Count - options.MaxRows} more event(s).");
            }

            lines.Add("");
            lines.Add($"Site: {options.SiteUrl}");
            lines.Add($"This report is generated automatically every {options.IntervalHours} hour(s).");

            return string.Join(Environment.NewLine, lines);
        }

        private static string BuildHtml(
            ActivityReportOptions options,
            DateTimeOffset startLocal,
            DateTimeOffset endLocal,
            string offsetLabel,
            IReadOnlyList<FumenActivity> activities,
            int submitted,
            int modified,
            int deleted)
        {
            var encoder = HtmlEncoder.Default;
            var rows = new List<string>();

            foreach (var activity in activities.Take(options.MaxRows))
            {
                var time = ToLocal(activity.OccurredAtUtc, options.Offset).ToString("yyyy-MM-dd HH:mm:ss");
                var type = TypeLabel(activity.Type);
                var color = activity.Type switch
                {
                    FumenActivityType.Submitted => "#0b63ce",
                    FumenActivityType.Modified => "#9a6700",
                    _ => "#c62828",
                };

                rows.Add($"""
                    <tr>
                      <td style="white-space:nowrap;vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;color:#5f6875;">{encoder.Encode(time)}</td>
                      <td style="white-space:nowrap;vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;color:{color};font-weight:600;">{encoder.Encode(type)}</td>
                      <td style="white-space:nowrap;vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;">{activity.MusicId}</td>
                      <td style="vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;">{encoder.Encode(activity.Title ?? "-")}</td>
                      <td style="vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;">{encoder.Encode(activity.Artist ?? "-")}</td>
                      <td style="vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;">{encoder.Encode(FormatOwner(activity))}</td>
                      <td style="vertical-align:top;padding:6px 8px;border-bottom:1px solid #e5e7eb;">{encoder.Encode(activity.Detail ?? "-")}</td>
                    </tr>
                    """);
            }

            var detailsSection = activities.Count == 0
                ? """
                  <div style="padding:14px 16px;background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;color:#166534;">
                    No submissions, file modifications, or deletions were detected in this period.
                  </div>
                  """
                : $"""
                  <table style="width:100%;border-collapse:collapse;font-size:13px;line-height:1.45;">
                    <thead>
                      <tr style="text-align:left;background:#f6f7f9;">
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">Time</th>
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">Type</th>
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">MusicId</th>
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">Title</th>
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">Artist</th>
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">Owner</th>
                        <th style="padding:7px 8px;border-bottom:1px solid #dfe3e8;color:#5f6875;">Detail</th>
                      </tr>
                    </thead>
                    <tbody>
                      {string.Join(Environment.NewLine, rows)}
                    </tbody>
                  </table>
                  {(activities.Count > options.MaxRows
                      ? $"<p style=\"margin:10px 0 0;color:#5f6875;\">... and {activities.Count - options.MaxRows} more event(s).</p>"
                      : string.Empty)}
                  """;

            return $"""
                <!doctype html>
                <html>
                <body style="margin:0;padding:0;background:#f6f7f9;color:#20242a;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif;">
                  <div style="max-width:960px;margin:0 auto;padding:24px 16px;">
                    <div style="background:#ffffff;border:1px solid #dfe3e8;border-radius:10px;padding:20px 22px;">
                      <h1 style="margin:0 0 4px;font-size:22px;line-height:1.3;">Fumen activity report</h1>
                      <p style="margin:0 0 18px;color:#5f6875;font-size:14px;">
                        {startLocal:yyyy-MM-dd HH:mm} - {endLocal:yyyy-MM-dd HH:mm} ({encoder.Encode(offsetLabel)})
                      </p>

                      <table style="border-collapse:collapse;margin:0 0 20px;">
                        <tr>
                          <td style="padding:8px 18px 8px 0;color:#5f6875;">New submissions</td>
                          <td style="padding:8px 0;font-size:18px;font-weight:700;">{submitted}</td>
                        </tr>
                        <tr>
                          <td style="padding:8px 18px 8px 0;color:#5f6875;">File modifications</td>
                          <td style="padding:8px 0;font-size:18px;font-weight:700;">{modified}</td>
                        </tr>
                        <tr>
                          <td style="padding:8px 18px 8px 0;color:#5f6875;">Deletions</td>
                          <td style="padding:8px 0;font-size:18px;font-weight:700;">{deleted}</td>
                        </tr>
                      </table>

                      <h2 style="margin:0 0 10px;font-size:17px;">Details</h2>
                      {detailsSection}

                      <p style="margin:22px 0 0;color:#5f6875;font-size:12px;line-height:1.6;">
                        Generated automatically every {options.IntervalHours} hour(s).<br>
                        Site: <a href="{encoder.Encode(options.SiteUrl)}" style="color:#0b63ce;">{encoder.Encode(options.SiteUrl)}</a>
                      </p>
                    </div>
                  </div>
                </body>
                </html>
                """;
        }

        private static string FormatOwner(FumenActivity activity)
        {
            var parts = new[] { activity.OwnerContact, activity.OwnerImContact }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
            return parts.Length == 0 ? "-" : string.Join(" / ", parts);
        }

        private static string TypeLabel(FumenActivityType type)
        {
            return type switch
            {
                FumenActivityType.Submitted => "Submitted",
                FumenActivityType.Modified => "Modified",
                FumenActivityType.Deleted => "Deleted",
                _ => type.ToString(),
            };
        }

        private static DateTimeOffset ToLocal(DateTime utc, TimeSpan offset)
        {
            var kind = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return new DateTimeOffset(kind).ToOffset(offset);
        }

        private static string FormatOffset(TimeSpan offset)
        {
            var sign = offset < TimeSpan.Zero ? "-" : "+";
            return sign + offset.Duration().ToString(@"hh\:mm");
        }
    }
}
