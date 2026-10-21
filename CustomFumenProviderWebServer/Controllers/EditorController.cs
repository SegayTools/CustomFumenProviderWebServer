using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Editor;
using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Mvc;

namespace CustomFumenProviderWebServer.Controllers
{
    [ApiController]
    [Route("editor")]
    public class EditorController : ControllerBase
    {
        private readonly IEditorService editorService;
        private readonly EditorUploadService editorUploadService;
        private readonly AdminAuthService adminAuthService;

        public EditorController(IEditorService editorService, EditorUploadService editorUploadService, AdminAuthService adminAuthService)
        {
            this.editorService = editorService;
            this.editorUploadService = editorUploadService;
            this.adminAuthService = adminAuthService;
        }

        [Route("get")]
        [HttpGet]
        public IActionResult Get(bool requireMasterBranch = true)
        {
            var editorResource = editorService.GetEditorResource(requireMasterBranch);

            if (!editorResource.IsAvaliable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "editor resource is preparing, please request again later");

            return File(editorResource.ZipContent, "application/zip", editorResource.LocalFileName);
        }

        [Route("getVersionInfo")]
        [HttpGet]
        public IActionResult GetVersionInfo(bool requireMasterBranch = true)
        {
            var editorResource = editorService.GetEditorResource(requireMasterBranch);

            if (!editorResource.IsAvaliable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "editor resource is preparing, please request again later");

            return new JsonResult(editorResource.VersionInfo);
        }

        /// <summary>
        /// 接收 GitHub Action 主动推送的编辑器 zip
        /// </summary>
        /// <param name="zipFormFile">名称必须匹配 OngekiFumenEditor_&lt;version&gt;_*.zip</param>
        /// <param name="password">管理员密码</param>
        /// <param name="branch">构建分支；master 会同时更新 master 资源</param>
        /// <returns>上传处理结果</returns>
        [HttpPut]
        [Route("updateEditorZip")]
        [RequestSizeLimit(FumenUploadLimits.MaxRequestBodySize)]
        public async Task<ActionResult<Result>> UpdateEditorZip([FromForm] IFormFile zipFormFile, [FromForm] string password, [FromForm] string branch = null)
        {
            if (!adminAuthService.VerifyAdminPassword(password))
                return StatusCode(StatusCodes.Status403Forbidden, new Result(false, "no permission"));

            if (zipFormFile is null || zipFormFile.Length <= 0)
                return BadRequest(new Result(false, "zip file is empty"));

            await using var stream = zipFormFile.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);

            var result = editorUploadService.Upload(zipFormFile.FileName, memory.ToArray(), branch, DateTime.UtcNow);
            if (!result.IsSuccess)
                return BadRequest(result);

            return result;
        }
    }
}
