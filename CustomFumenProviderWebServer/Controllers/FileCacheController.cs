using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.CacheManager;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Text.Json;

namespace CustomFumenProviderWebServer.Controllers
{
    [ApiController]
    [Route("fileCache")]
    public class FileCacheController : ControllerBase
    {
        private readonly ILogger<FileCacheController> logger;
        private readonly IFileCacheListService fileCacheListService;

        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public FileCacheController(ILogger<FileCacheController> logger, IFileCacheListService fileCacheListService)
        {
            this.logger = logger;
            this.fileCacheListService = fileCacheListService;
        }

        [Route("list")]
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.Any, NoStore = true)]
        public FileCacheListResponse ListAll()
        {
            var cacheFumenInfoList = fileCacheListService.CacheFumenMap.Values.ToArray();
            return new FileCacheListResponse()
            {
                CacheFumenInfos = cacheFumenInfoList,
            };
        }
    }
}
