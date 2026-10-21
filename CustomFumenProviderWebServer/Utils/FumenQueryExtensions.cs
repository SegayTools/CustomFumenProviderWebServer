using CustomFumenProviderWebServer.Models.Tables;

namespace CustomFumenProviderWebServer.Utils
{
    public static class FumenQueryExtensions
    {
        /// <summary>
        /// 按定数过滤谱面。只提供 min 或 max 时按单边过滤；
        /// 同时提供时必须是"同一个难度"落在 [min, max] 内，而不是两个难度分别满足上下界。
        /// </summary>
        public static IQueryable<FumenSet> ApplyLevelFilter(this IQueryable<FumenSet> query, float? minFilterLevel, float? maxFilterLevel)
        {
            if (minFilterLevel is float minLevel && maxFilterLevel is float maxLevel)
                return query.Where(x => x.FumenDifficults.Any(d => d.Level >= minLevel && d.Level <= maxLevel));

            if (minFilterLevel is float minOnly)
                return query.Where(x => x.FumenDifficults.Any(d => d.Level >= minOnly));

            if (maxFilterLevel is float maxOnly)
                return query.Where(x => x.FumenDifficults.Any(d => d.Level <= maxOnly));

            return query;
        }
    }
}
