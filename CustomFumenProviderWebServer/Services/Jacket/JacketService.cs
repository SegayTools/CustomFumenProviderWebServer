using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Utils;
using System.Diagnostics;
using System.IO.Compression;
using static CustomFumenProviderWebServer.Utils.ProcessExec;

namespace CustomFumenProviderWebServer.Services.Jacket
{
    public class JacketService
    {
        private readonly ILogger<JacketService> logger;
        private string binPath;

        public JacketService(ILogger<JacketService> logger)
        {
            this.logger = logger;

            var binFolder = "";
            var resourcePath = "";

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                binFolder = Path.GetFullPath("./JacketGeneratorBin");
                resourcePath = "CustomFumenProviderWebServer.Resources.Jacket.linux.zip";
                binPath = Path.Combine(binFolder, "JacketGenerator");
            }
            else
            {
                binFolder = Path.GetTempFileName().Replace(".", string.Empty) + "_JacketGenerator";
                resourcePath = "CustomFumenProviderWebServer.Resources.Jacket.win.zip";
                binPath = Path.Combine(binFolder, "JacketGenerator.exe");
            }

            File.Delete(binFolder);
            Directory.CreateDirectory(binFolder);

            using var zip = new ZipArchive(typeof(JacketService).Assembly.GetManifestResourceStream(resourcePath));
            logger.LogInformation($"extract {resourcePath} to {binFolder}");
            zip.ExtractToDirectory(binFolder, true);
            logger.LogInformation($"JacketGenerator bin file: {binPath}");
        }

        private Task<ExecResult> Exec(params string[] args)
        {
            return ProcessExec.Exec(binPath, args);
        }

        public async Task<Result> GenerateAssetbundleJacket(string inputPngFile, bool isSmall, int width, int height, string outputFolder)
        {
            var args = new[] {
                "--inputFiles", inputPngFile,
                "--outputFolder", outputFolder,
                "--width", width.ToString(),
                "--height", height.ToString(),
                "--mode", "GenerateJacket",
                "--gameType","SDDT",
                "--noPause",
            };
            var result = await Exec(args);

            if (result.Output?.Contains("SUCCESS") ?? false)
                return new(true);

            return new Result(false);
        }
    }
}
