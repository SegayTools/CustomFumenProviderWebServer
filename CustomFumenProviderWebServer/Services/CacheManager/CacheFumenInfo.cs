namespace CustomFumenProviderWebServer.Services.CacheManager
{
    public class CacheFumenInfo
    {
        public int MusicId { get; set; }
        public DateTime UpdateTime { get; set; }

        public List<CacheFileInfo> CacheFileInfo { get; set; } = new();
    }
}
