using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Models
{
    public class GithubActionListAction
    {
        [JsonPropertyName("workflow_runs")]
        public List<GithubActionWorkflowRun> WorkflowRuns { get; set; } = new();
    }
}
