using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.Email;

namespace CustomFumenProviderWebServer.Tests;

public class ActivityReportBuilderTests
{
    [Fact]
    public void Build_NoEvents_ProducesZeroSummaryAndNoActivityMessage()
    {
        var options = EmailTestHelpers.CreateReportOptions();
        var start = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(6);

        var email = ActivityReportBuilder.Build(options, start, end, Array.Empty<FumenActivity>());

        Assert.Single(email.To);
        Assert.Contains("0 new, 0 modified, 0 deleted", email.Subject);
        Assert.Contains("No submissions, file modifications, or deletions", email.TextBody);
        Assert.Contains("No submissions, file modifications, or deletions", email.HtmlBody);
    }

    [Fact]
    public void Build_GroupsActivityCountsAndIncludesDetails()
    {
        var options = EmailTestHelpers.CreateReportOptions();
        var start = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(6);
        var activities = new[]
        {
            new FumenActivity
            {
                MusicId = 20001,
                Title = "New Song",
                Artist = "Artist A",
                OwnerContact = "owner@example.com",
                Type = FumenActivityType.Submitted,
                Detail = "new submission",
                OccurredAtUtc = start.AddMinutes(10),
            },
            new FumenActivity
            {
                MusicId = 20001,
                Title = "New Song",
                Artist = "Artist A",
                Type = FumenActivityType.Modified,
                Detail = "audio",
                OccurredAtUtc = start.AddHours(1),
            },
            new FumenActivity
            {
                MusicId = 20002,
                Title = "Old Song",
                Artist = "Artist B",
                Type = FumenActivityType.Deleted,
                Detail = "deleted",
                OccurredAtUtc = start.AddHours(2),
            },
        };

        var email = ActivityReportBuilder.Build(options, start, end, activities);

        Assert.Contains("1 new, 1 modified, 1 deleted", email.Subject);
        Assert.Contains("New submissions:   1", email.TextBody);
        Assert.Contains("File modifications: 1", email.TextBody);
        Assert.Contains("Deletions:         1", email.TextBody);
        Assert.Contains("New Song", email.TextBody);
        Assert.Contains("Old Song", email.TextBody);
        Assert.Contains("owner@example.com", email.TextBody);
        Assert.Contains("Submitted", email.HtmlBody);
        Assert.Contains("Modified", email.HtmlBody);
        Assert.Contains("Deleted", email.HtmlBody);
    }

    [Fact]
    public void Build_EscapesUserControlledValuesInHtml()
    {
        var options = EmailTestHelpers.CreateReportOptions();
        var start = new DateTime(2026, 11, 2, 18, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(6);
        var activities = new[]
        {
            new FumenActivity
            {
                MusicId = 20001,
                Title = "<script>alert(1)</script>",
                Type = FumenActivityType.Submitted,
                Detail = "<b>detail</b>",
                OccurredAtUtc = start.AddMinutes(1),
            },
        };

        var email = ActivityReportBuilder.Build(options, start, end, activities);

        Assert.DoesNotContain("<script>alert(1)</script>", email.HtmlBody);
        Assert.DoesNotContain("<b>detail</b>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
    }
}
