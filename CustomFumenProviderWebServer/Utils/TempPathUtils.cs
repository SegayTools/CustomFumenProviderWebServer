namespace CustomFumenProviderWebServer.Utils
{
    public static class TempPathUtils
    {
        public static string GetNewTempFolder()
        {
            var path = Path.GetTempFileName();
            File.Delete(path);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
