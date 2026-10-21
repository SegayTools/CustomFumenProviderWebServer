using CustomFumenProviderWebServer.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;

namespace CustomFumenProviderWebServer.Services.Editor
{
    public class EditorServiceUpdater : IHostedService
    {
        // 仓库里还有部署浏览器的 workflow；取全仓库最新 run 会拿到 browser-site-mt.zip 这类无关产物。
        private const string EditorWorkflow = "BuildProgram.yml";
        private const string RepositoryApiUrl = "https://api.github.com/repos/NyagekiFumenProject/OngekiFumenEditor";

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
                var latestRun = await GetLatestSuccessfulRun(false);
                var masterRun = await GetLatestSuccessfulRun(true);

                if (latestRun is not null && masterRun is not null && latestRun.Id == masterRun.Id)
                {
                    // 两个通道指向同一次构建，只下载一次。
                    await PublishRun(latestRun, requireMasterBranch: true);
                    return;
                }

                if (latestRun is not null)
                    await PublishRun(latestRun, requireMasterBranch: false);
                if (masterRun is not null)
                    await PublishRun(masterRun, requireMasterBranch: true);
            }
            catch (Exception e)
            {
                logger.LogError(e, "fetch editor resource failed");
            }
        }

        private async Task<GithubActionWorkflowRun> GetLatestSuccessfulRun(bool requireMasterBranch)
        {
            try
            {
                var url = $"{RepositoryApiUrl}/actions/workflows/{EditorWorkflow}/runs?status=success&per_page=1";
                if (requireMasterBranch)
                    url += "&branch=master";

                using var resp = await httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();

                var runsResp = await resp.Content.ReadFromJsonAsync<GithubActionListAction>();
                var run = runsResp?.WorkflowRuns?.FirstOrDefault();
                if (run is null)
                    return null;

                var currentResource = editorService.GetEditorResource(requireMasterBranch);
                return run.CreatedAt > currentResource.LocalCreateAt ? run : null;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Can't fetch latest editor workflow run, requireMasterBranch:{RequireMasterBranch}", requireMasterBranch);
                return null;
            }
        }

        private async Task PublishRun(GithubActionWorkflowRun run, bool requireMasterBranch)
        {
            try
            {
                using var artifactsResp = await httpClient.GetAsync(run.ArtifactsUrl);
                artifactsResp.EnsureSuccessStatusCode();

                var artifacts = await artifactsResp.Content.ReadFromJsonAsync<GithubActionListArtifacts>();
                if (!EditorArtifactSelector.TrySelect(artifacts?.Artifacts, out var artifact, out var version))
                {
                    logger.LogWarning("no usable editor artifact in workflow run, requireMasterBranch:{RequireMasterBranch}, run:{RunId}, date:{Date}",
                        requireMasterBranch, run.Id, run.CreatedAt);
                    return;
                }

                using var downloadResp = await httpClient.GetAsync(artifact.ArchiveDownloadUrl);
                if (downloadResp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // GitHub 的 artifact 下载端点即使对公开仓库也要求认证。
                    logger.LogError("GitHub rejected the editor artifact download (401); set EDITOR_UPDATE_GITHUB_TOKEN with actions:read permission, requireMasterBranch:{RequireMasterBranch}",
                        requireMasterBranch);
                    return;
                }

                downloadResp.EnsureSuccessStatusCode();

                var content = await downloadResp.Content.ReadAsByteArrayAsync();
                if (!EditorZip.IsValid(content))
                {
                    logger.LogError("downloaded editor artifact is not a valid zip, requireMasterBranch:{RequireMasterBranch}, artifact:{Artifact}",
                        requireMasterBranch, artifact.Name);
                    return;
                }

                var branch = string.IsNullOrWhiteSpace(run.HeadBranch) ? "unknown" : run.HeadBranch.Replace('/', '_');
                var resource = new EditorResource
                {
                    LocalFileName = $"OngekiFumenEditor_{version}_{branch}.zip",
                    ZipContent = content,
                    LocalCreateAt = run.CreatedAt,
                    VersionInfo = new VersionInfo
                    {
                        Time = run.CreatedAt,
                        Branch = run.HeadBranch,
                        Version = version,
                        FileSize = content.Length,
                    },
                };

                if (editorService.UpdateEditorResource(requireMasterBranch, resource))
                    logger.LogInformation("fetch editor new version, requireMasterBranch:{RequireMasterBranch}, version:{Version}, file:{FileName}, branch:{Branch}",
                        requireMasterBranch, version, resource.LocalFileName, run.HeadBranch);
                else
                    logger.LogInformation("ignore editor artifact with non-newer version, requireMasterBranch:{RequireMasterBranch}, version:{Version}, file:{FileName}, branch:{Branch}",
                        requireMasterBranch, version, resource.LocalFileName, run.HeadBranch);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Can't fetch latest editor zip from github action, requireMasterBranch:{RequireMasterBranch}", requireMasterBranch);
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
