using System.Net;
using CustomFumenProviderWebServer.Controllers;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Gmail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CustomFumenProviderWebServer.Tests;

public class GoogleOAuthControllerTests
{
    [Fact]
    public async Task Callback_WithValidState_StoresTokenAndInvalidatesCachedAccessToken()
    {
        var options = GmailTestHelpers.CreateOptions();
        var store = new FakeRefreshTokenStore();
        var accountStore = new FakeGmailAccountStore();
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(GmailTestHelpers.Json(HttpStatusCode.OK, """{"refresh_token":"refresh-1"}""")));
        var authorizationService = new GmailAuthorizationService(
            new StubHttpClientFactory(handler),
            store,
            accountStore,
            options,
            NullLogger<GmailAuthorizationService>.Instance);
        var stateStore = new GoogleOAuthStateStore(TimeProvider.System);
        var tokenProvider = new FakeAccessTokenProvider("cached-access-token");

        var controller = new GoogleOAuthController(
            options,
            stateStore,
            authorizationService,
            tokenProvider,
            accountStore,
            new AdminAuthService(NullLogger<AdminAuthService>.Instance),
            NullLogger<GoogleOAuthController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };

        var state = stateStore.Create();
        var result = await controller.Callback("code-1", state);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal("refresh-1", store.Token);
        Assert.Equal(1, tokenProvider.InvalidateCount);
    }
}
