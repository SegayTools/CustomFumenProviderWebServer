using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

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
        /// <param name="genre">需要过滤显示的分类，null则表示不过滤</param>
        /// <param name="order">需要排序的字段，null表示不排序</param>
        /// <param name="minFilterLevel"></param>
        /// <param name="maxFilterLevel"></param>
        /// <returns></returns>
        [Route("list")]
        [HttpGet]
        public async Task<FumenQueryResponse> List(int pageIdx, int countPerPage, string genre = null, string order = null, float? minFilterLevel = null, float? maxFilterLevel = null)
        {
            var offset = pageIdx * countPerPage;

            using var db = await fumenDataDBFactory.CreateDbContextAsync();

            IQueryable<FumenSet> list = db.FumenSets;

            if (!string.IsNullOrWhiteSpace(genre))
            {
                list = list.Where(x => x.Genre == genre);
            }

            if (!string.IsNullOrWhiteSpace(order))
            {
                list = list.OrderBy(x => EF.Property<FumenSet>(x, order));
            }

            var fumenSets = await list.Skip(offset).Take(countPerPage).Include(x => x.FumenDifficults).ToListAsync();

            if (minFilterLevel is float minLevel)
            {
                for (int setIdx = 0; setIdx < fumenSets.Count; setIdx++)
                {
                    var set = fumenSets[setIdx];
                    for (int diffIdx = 0; diffIdx < set.FumenDifficults.Count; diffIdx++)
                    {
                        var diff = set.FumenDifficults.ElementAt(diffIdx);
                        if (diff.Level < minLevel)
                        {
                            set.FumenDifficults.Remove(diff);
                            diffIdx--;
                        }
                    }

                    if (set.FumenDifficults.Count == 0)
                        fumenSets.RemoveAt(setIdx--);
                }
            }

            if (maxFilterLevel is float maxLevel)
            {
                for (int setIdx = 0; setIdx < fumenSets.Count; setIdx++)
                {
                    var set = fumenSets[setIdx];
                    for (int diffIdx = 0; diffIdx < set.FumenDifficults.Count; diffIdx++)
                    {
                        var diff = set.FumenDifficults.ElementAt(diffIdx);
                        if (diff.Level > maxLevel)
                        {
                            set.FumenDifficults.Remove(diff);
                            diffIdx--;
                        }
                    }

                    if (set.FumenDifficults.Count == 0)
                        fumenSets.RemoveAt(setIdx--);
                }
            }

            var response = new FumenQueryResponse();
            response.FumenSets = fumenSets;

            return response;
        }
    }
}
