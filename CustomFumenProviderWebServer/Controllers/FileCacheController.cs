using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.CacheManager;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Collections.Immutable;
using System.Text.Json;

namespace CustomFumenProviderWebServer.Controllers
{
    [ApiController]
    [Route("fileCache")]
    public class FileCacheController : ControllerBase
    {
        private readonly ILogger<FileCacheController> logger;
        private readonly IFileCacheListService fileCacheListService;
        private readonly IDbContextFactory<FumenDataDB> dbContextFactory;
        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public FileCacheController(ILogger<FileCacheController> logger, IFileCacheListService fileCacheListService, IDbContextFactory<FumenDataDB> dbContextFactory)
        {
            this.logger = logger;
            this.fileCacheListService = fileCacheListService;
            this.dbContextFactory = dbContextFactory;
        }

        [Route("list")]
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.Any, NoStore = true)]
        public async Task<FileCacheListResponse> List(bool onlyPublish = true)
        {
            var cacheFumenInfoList = fileCacheListService.CacheFumenMap.Values;

            if (onlyPublish)
            {
                using var db = await dbContextFactory.CreateDbContextAsync();
                var publishMusicIds = (await db.FumenSets.Where(x => x.PublishState == Models.PublishState.Published).Select(x => x.MusicId).ToListAsync()).ToImmutableHashSet();
                cacheFumenInfoList = cacheFumenInfoList.Where(f => publishMusicIds.Contains(f.MusicId)).ToList();
            }

            return new FileCacheListResponse()
            {
                CacheFumenInfos = cacheFumenInfoList.ToArray(),
            };
        }

        [Route("forceUpdate")]
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.Any, NoStore = true)]
        public async Task<Result> ForceUpdate(bool onlyPublish = true)
        {
            fileCacheListService.ForceRebuildAll();
        }
    }
}
