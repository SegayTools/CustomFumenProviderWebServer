using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Controllers
{

    [ApiController]
    [Route("fumen")]
    public partial class FumenController : ControllerBase
    {
        private readonly ILogger<FumenController> logger;
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly string fumenFolderPath;

        public FumenController(ILogger<FumenController> logger, IDbContextFactory<FumenDataDB> fumenDataDBFactory)
        {
            this.logger = logger;
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.fumenFolderPath = Environment.GetEnvironmentVariable("FumenDirectory");
        }

        /// <summary>
        /// 获取谱面列表
        /// </summary>
        /// <param name="pageIdx">页号 (从0开始)</param>
        /// <param name="countPerPage">每一页显示多少个谱面</param>
        /// <returns></returns>
        [Route("list")]
        [HttpGet]
        public async Task<FumenQueryResponse> List(int pageIdx, int countPerPage)
        {
            var offset = pageIdx * countPerPage;

            using var db = await fumenDataDBFactory.CreateDbContextAsync();

            var result = await db.FumenSets.Skip(offset).Take(countPerPage).Include(x => x.FumenDifficults).ToListAsync();

            var response = new FumenQueryResponse();
            response.FumenSets = result;

            return response;
        }
    }
}
