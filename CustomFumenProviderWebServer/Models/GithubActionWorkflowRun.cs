using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models
{
    public class GithubActionWorkflowRun
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("artifacts_url")]
        public string ArtifactsUrl { get; set; }

        [JsonPropertyName("head_branch")]
        public string HeadBranch { get; set; }
    }
}