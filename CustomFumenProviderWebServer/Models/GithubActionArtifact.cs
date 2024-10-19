using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models
{
    public class GithubActionArtifact
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("archive_download_url")]
        public string ArchiveDownloadUrl { get; set; }
    }
}