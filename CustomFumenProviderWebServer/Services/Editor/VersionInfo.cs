namespace CustomFumenProviderWebServer.Services.Editor
{
    public class VersionInfo
    {
        public string Branch { get; set; }
        public DateTime Time { get; set; }
        public Version Version { get; set; }
        public int FileSize { get; set; }
    }
}
