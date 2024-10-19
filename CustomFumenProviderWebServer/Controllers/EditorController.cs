using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace CustomFumenProviderWebServer.Controllers
{
    [ApiController]
    [Route("editor")]
    public class EditorController : ControllerBase
    {
        private readonly string folder;
        private readonly HttpClient httpClient;

        private static DateTime localCreateAt = DateTime.MinValue;
        private static byte[] zipContent = null;
        private static string localFileName = null;

        public EditorController()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new("Bearer", "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");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CSharpApp");
        }

        [Route("get")]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var (content, fileName) = await TryGetLatestEditorZip();
            return File(content, "application/zip", fileName);
        }

        private async Task<(byte[] content, string fileName)> TryGetLatestEditorZip()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, @"https://api.github.com/repos/NyagekiFumenProject/OngekiFumenEditor/actions/runs?status=success&per_page=1&branch=master");
            var resp = await httpClient.SendAsync(req);

            var runsResp = await resp.Content.ReadFromJsonAsync<GithubActionListAction>();
            if (runsResp?.WorkflowRuns.FirstOrDefault() is GithubActionWorkflowRun workflowRun)
            {
                if (workflowRun.CreatedAt > localCreateAt)
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

                        var content =  await resp.Content.ReadAsByteArrayAsync();

                        //update
                        localFileName = artifact.Name + ".zip";
                        zipContent = content;
                        localCreateAt = workflowRun.CreatedAt;
                    }
                }
            }

            if (zipContent != null)
                return (zipContent, localFileName);
            throw new Exception("Can't fetch latest editor zip from github action.");
        }
    }
}
