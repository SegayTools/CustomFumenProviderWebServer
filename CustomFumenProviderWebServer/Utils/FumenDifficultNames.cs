namespace CustomFumenProviderWebServer.Utils
{
    /// <summary>
    /// 难度下标与名称/归档文件名的唯一映射。
    /// 下标含义与游戏端 opt 文件 {musicId}_0{index}.ogkr 保持一致。
    /// </summary>
    public static class FumenDifficultNames
    {
        /// <summary>0..4 对应的显示名，越界返回空串。</summary>
        public static string GetName(int diffIdx)
        {
            return diffIdx switch
            {
                0 => "Basic",
                1 => "Advanced",
                2 => "Expert",
                3 => "Master",
                4 => "Lunatic",
                _ => string.Empty
            };
        }

        /// <summary>0..4 对应 fumen{musicId} 根目录下的原始谱面归档名。</summary>
        public static string GetArchiveFileName(int diffIdx)
        {
            return diffIdx switch
            {
                0 => "basic.ogkr",
                1 => "advanced.ogkr",
                2 => "expert.ogkr",
                3 => "master.ogkr",
                4 => "lunatic.ogkr",
                _ => string.Empty
            };
        }
    }
}
