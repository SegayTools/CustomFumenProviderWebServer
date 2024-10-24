
using CustomFumenProviderWebServer.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace CustomFumenProviderWebServer.Services.Editor
{
    public class EditorServiceUpdater : IHostedService
    {
        private readonly string folder;
        private readonly HttpClient httpClient;
        private readonly ILogger<EditorService> logger;
        private readonly IEditorService editorService;
        private readonly Regex versionRegex = new Regex(@"OngekiFumenEditor_([\d\.]+)_");
        private Timer timer;

        public EditorServiceUpdater(ILogger<EditorService> logger, IEditorService editorService)
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", "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");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CSharpApp");

            this.logger = logger;
            this.editorService = editorService;
        }

        private async void OnFetchUpdate()
        {
            try
            {
                logger.LogInformation($"start fetch editor resource");
                await Task.WhenAll([
                    UpdateEditorResource(editorService.GetEditorResource(false), false),
                    UpdateEditorResource(editorService.GetEditorResource(true), true)
                ]);
            }
            catch (Exception e)
            {
            }
            finally
            {
            }
        }

        private async Task UpdateEditorResource(EditorResource editorResource, bool requireMasterBranch)
        {
            try
            {
                var url = @"https://api.github.com/repos/NyagekiFumenProject/OngekiFumenEditor/actions/runs?status=success&per_page=1";
                if (requireMasterBranch)
                    url += "&branch=master";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await httpClient.SendAsync(req);

                var runsResp = await resp.Content.ReadFromJsonAsync<GithubActionListAction>();
                if (runsResp?.WorkflowRuns.FirstOrDefault() is GithubActionWorkflowRun workflowRun)
                {
                    if (workflowRun.CreatedAt > editorResource.LocalCreateAt)
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

                            //update
                            editorResource.LocalFileName = artifact.Name + ".zip";
                            editorResource.ZipContent = content;
                            editorResource.LocalCreateAt = workflowRun.CreatedAt;

                            editorResource.VersionInfo = new()
                            {
                                Time = workflowRun.CreatedAt,
                                Branch = workflowRun.HeadBranch,
                                FileSize = content.Length
                            };

                            var match = versionRegex.Match(artifact.Name);
                            if (match.Success)
                                editorResource.VersionInfo.Version = Version.Parse(match.Groups[1].Value);
                            else
                                editorResource.VersionInfo.Version = default;

                            logger.LogInformation($"fetch editor new version, requireMasterBranch:{requireMasterBranch}, version:{editorResource.VersionInfo.Version}, date:{editorResource.LocalFileName}, branch:{editorResource.VersionInfo.Branch}");
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
            timer = new Timer(_ => OnFetchUpdate(), default, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await timer.DisposeAsync();
        }
    }
}
