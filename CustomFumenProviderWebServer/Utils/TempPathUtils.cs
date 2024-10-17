namespace CustomFumenProviderWebServer.Utils
{
    public static class TempPathUtils
    {
        public static string GetNewTempFolder()
        {
            var path = Path.GetTempFileName().Replace(".", string.Empty);
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
