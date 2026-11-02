#nullable enable

using CustomFumenProviderWebServer.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CustomFumenProviderWebServer.Tests;

public class HomeControllerTests
{
    [Fact]
    public void RedirectToFrontend_ReturnsConfiguredFrontendUrl()
    {
        var controller = new HomeController();

        var result = controller.RedirectToFrontend();

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://next.nageki-net.com/net/custom-fumen-list", redirect.Url);
    }
}
