using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.Editor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json.Serialization;

namespace CustomFumenProviderWebServer.Controllers
{
    [ApiController]
    [Route("editor")]
    public class EditorController : ControllerBase
    {
        private readonly IEditorService editorService;

        public EditorController(IEditorService editorService)
        {
            this.editorService = editorService;
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
    }
}
