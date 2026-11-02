#nullable enable

namespace CustomFumenProviderWebServer.Models.Responses
{
    public sealed record GmailAuthorizationStatus(
        bool Configured,
        bool Authorized,
        string SenderAddress,
        string RedirectUri);
}
