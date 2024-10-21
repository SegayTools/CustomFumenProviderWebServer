namespace CustomFumenProviderWebServer.Services.Editor
{
    public class EditorResource
    {
        public DateTime LocalCreateAt { get; set; } = DateTime.MinValue;
        public byte[] ZipContent { get; set; } = null;
        public string LocalFileName { get; set; } = null;

        public VersionInfo VersionInfo { get; set; } = null;

        public bool IsAvaliable => ZipContent is not null;
    }
}
