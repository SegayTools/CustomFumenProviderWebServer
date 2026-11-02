#nullable enable

using Microsoft.AspNetCore.Mvc;

namespace CustomFumenProviderWebServer.Controllers
{
    /// <summary>
    /// 根页面跳转到新的前端列表页。
    /// </summary>
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class HomeController : ControllerBase
    {
        private const string FrontendUrl = "https://next.nageki-net.com/net/custom-fumen-list";

        [HttpGet("/")]
        [HttpGet("/index.html")]
        public IActionResult RedirectToFrontend()
        {
            return Redirect(FrontendUrl);
        }
    }
}
