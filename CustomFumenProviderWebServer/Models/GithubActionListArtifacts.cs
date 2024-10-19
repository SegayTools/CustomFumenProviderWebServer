using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models
{
    public class GithubActionListArtifacts
    {
        [JsonPropertyName("artifacts")]
        public List<GithubActionArtifact> Artifacts { get; set; } = new();
    }
}
