using CustomFumenProviderWebServer.Controllers;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CustomFumenProviderWebServer.Tests;

public class GoogleOAuthPublicControllerTests
{
    [Fact]
    public void HomePage_IsPublicAndLinksReviewDocuments()
    {
        var content = Render(controller => controller.Home());

        Assert.Contains("CustomFumenChangedNotify", content);
        Assert.Contains("No Google sign-in", content);
        Assert.Contains("/oauth/google/public/purpose", content);
        Assert.Contains("/oauth/google/public/privacy", content);
        Assert.Contains("/oauth/google/public/terms", content);
        Assert.Contains("not a marketing, bulk-email", content);
    }

    [Fact]
    public void PrivacyPolicy_ContainsDataUsageAndRevocationInformation()
    {
        var content = Render(controller => controller.Privacy());

        Assert.Contains("Google API Services User Data Policy", content);
        Assert.Contains("gmail.send", content);
        Assert.Contains("https://myaccount.google.com/permissions", content);
        Assert.Contains("non-consensual intimate imagery", content);
    }

    [Fact]
    public void TermsPage_ContainsProhibitedUseAndLimitedUse()
    {
        var content = Render(controller => controller.Terms());

        Assert.Contains("Limited Use", content);
        Assert.Contains("non-consensual intimate imagery", content);
        Assert.Contains("Google API Services User Data Policy", content);
    }

    [Fact]
    public void PurposePage_ExplainsEachScope()
    {
        var content = Render(controller => controller.Purpose());

        Assert.Contains("<code>openid</code>", content);
        Assert.Contains("<code>email</code>", content);
        Assert.Contains("https://www.googleapis.com/auth/gmail.send", content);
        Assert.Contains("does not request Gmail read", content);
    }

    private static string Render(Func<GoogleOAuthPublicController, IActionResult> action)
    {
        var controller = new GoogleOAuthPublicController(new GmailOptions
        {
            PublicAppName = "CustomFumenChangedNotify",
            PublicContactEmail = "support@example.com",
        })
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        var result = action(controller);
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal("text/html; charset=utf-8", content.ContentType);
        Assert.NotNull(content.Content);
        return content.Content!;
    }
}
