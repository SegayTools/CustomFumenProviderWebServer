using CustomFumenProviderWebServer.Models.Tables;

namespace CustomFumenProviderWebServer.Utils
{
    public static class FumenSetNormalizer
    {
        /// <summary>
        /// 把 Music.xml 解析出的谱面统一挂到目标 musicId 下，避免只改父实体后
        /// 难度行仍引用 XML 里的旧 id。
        /// </summary>
        public static void ApplyMusicId(FumenSet set, int musicId)
        {
            set.MusicId = musicId;
            foreach (var difficult in set.FumenDifficults)
                difficult.MusicId = musicId;
        }
    }
}
