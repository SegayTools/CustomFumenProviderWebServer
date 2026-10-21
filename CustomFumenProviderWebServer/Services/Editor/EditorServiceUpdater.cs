using CustomFumenProviderWebServer.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace CustomFumenProviderWebServer.Services.Editor
{
    public class EditorServiceUpdater : IHostedService
    {
        private readonly HttpClient httpClient;
        private readonly ILogger<EditorService> logger;
        private readonly IEditorService editorService;
        private Timer timer;

        public EditorServiceUpdater(ILogger<EditorService> logger, IEditorService editorService)
        {
            httpClient = new HttpClient();
            var token = Environment.GetEnvironmentVariable("EDITOR_UPDATE_GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CSharpApp");

            this.logger = logger;
            this.editorService = editorService;
        }

        private async void OnFetchUpdate()
        {
            try
            {
                await Task.WhenAll([
                    UpdateEditorResource(false),
                    UpdateEditorResource(true)
                ]);
            }
            catch (Exception e)
            {
                logger.LogError(e, "fetch editor resource failed");
            }
        }

        private async Task UpdateEditorResource(bool requireMasterBranch)
        {
            try
            {
                var currentResource = editorService.GetEditorResource(requireMasterBranch);

                var url = @"https://api.github.com/repos/NyagekiFumenProject/OngekiFumenEditor/actions/runs?status=success&per_page=1";
                if (requireMasterBranch)
                    url += "&branch=master";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await httpClient.SendAsync(req);

                var runsResp = await resp.Content.ReadFromJsonAsync<GithubActionListAction>();
                if (runsResp?.WorkflowRuns.FirstOrDefault() is GithubActionWorkflowRun workflowRun)
                {
                    if (workflowRun.CreatedAt > currentResource.LocalCreateAt)
                    {
                        //get artifact
                        req = new HttpRequestMessage(HttpMethod.Get, workflowRun.ArtifactsUrl);
                        resp = await httpClient.SendAsync(req);

                        var artifactsResp = await resp.Content.ReadFromJsonAsync<GithubActionListArtifacts>();
                        if (artifactsResp?.Artifacts.FirstOrDefault() is GithubActionArtifact artifact)
                        {
                            //download zip
                            req = new HttpRequestMessage(HttpMethod.Get, artifact.ArchiveDownloadUrl);
                            resp = await httpClient.SendAsync(req);

                            var content = await resp.Content.ReadAsByteArrayAsync();

                            var resource = new EditorResource
                            {
                                LocalFileName = artifact.Name + ".zip",
                                ZipContent = content,
                                LocalCreateAt = workflowRun.CreatedAt,
                                VersionInfo = new VersionInfo
                                {
                                    Time = workflowRun.CreatedAt,
                                    Branch = workflowRun.HeadBranch,
                                    FileSize = content.Length,
                                },
                            };

                            if (EditorArtifactName.TryParse(artifact.Name, out var version))
                                resource.VersionInfo.Version = version;

                            if (editorService.UpdateEditorResource(requireMasterBranch, resource))
                                logger.LogInformation($"fetch editor new version, requireMasterBranch:{requireMasterBranch}, version:{resource.VersionInfo.Version}, date:{resource.LocalFileName}, branch:{resource.VersionInfo.Branch}");
                            else
                                logger.LogInformation($"ignore editor artifact with non-newer version, requireMasterBranch:{requireMasterBranch}, version:{resource.VersionInfo.Version}, date:{resource.LocalFileName}");
                            return;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError($"Can't fetch latest editor zip from github action, requireMasterBranch:{requireMasterBranch}, exceptionMessage: {e.Message}");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var pollingEnabled = Environment.GetEnvironmentVariable("EDITOR_UPDATE_POLLING_ENABLED");
            if (string.Equals(pollingEnabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("editor GitHub polling is disabled; waiting for pushed zip");
                return Task.CompletedTask;
            }

            timer = new Timer(_ => OnFetchUpdate(), default, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (timer is null)
                return;

            await timer.DisposeAsync();
        }
    }
}
