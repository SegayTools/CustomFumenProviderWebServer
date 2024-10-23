using AssetsTools.NET;
using AssetsTools.NET.Extra;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Utils;
using System.Diagnostics;
using System.IO.Compression;
using TexturePlugin;
using static CustomFumenProviderWebServer.Utils.ProcessExec;

namespace CustomFumenProviderWebServer.Services
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

            binFolder = Path.GetTempFileName() + "_JacketGenerator";

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                resourcePath = "CustomFumenProviderWebServer.Resources.Jacket.linux.zip";
                binPath = Path.Combine(binFolder, "JacketGenerator");
            }
            else
            {
                resourcePath = "CustomFumenProviderWebServer.Resources.Jacket.win.zip";
                binPath = Path.Combine(binFolder, "JacketGenerator.exe");
            }

            File.Delete(binFolder);
            Directory.CreateDirectory(binFolder);

            using var zip = new ZipArchive(typeof(JacketService).Assembly.GetManifestResourceStream(resourcePath));
            logger.LogInformation($"extract {resourcePath} to {binFolder}");
            zip.ExtractToDirectory(binFolder, true);
            logger.LogInformation($"JacketGenerator bin file: {binPath}");

            if (Environment.OSVersion.Platform == PlatformID.Unix)
                File.SetUnixFileMode(binPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        private Task<ExecResult> Exec(params string[] args)
        {
            return ProcessExec.Exec(binPath, args);
        }

        public async Task<Result> GenerateAssetbundleJacket(string inputPngFile, int width, int height, string outputFolder)
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

        public Task<Result> DumpPngFromAssetbundle(string inputABFile, string outputPngFile)
        {
            return Task.Run<Result>(() =>
            {
                var assetManager = new AssetsManager();

                using var fs = File.OpenRead(inputABFile);

                var assetBundleFile = assetManager.LoadBundleFile(fs, inputABFile);
                var assetsFile = assetManager.LoadAssetsFileFromBundle(assetBundleFile, 0);
                var assetsTable = assetsFile.table;

                var assetInfos = assetsTable.GetAssetsOfType(0x1C);
                foreach (var assetInfo in assetInfos)
                {
                    var baseField = assetManager.GetTypeInstance(assetsFile.file, assetInfo).GetBaseField();

                    var width = baseField["m_Width"].GetValue().AsInt();
                    var height = baseField["m_Height"].GetValue().AsInt();
                    var format = (TextureFormat)baseField["m_TextureFormat"].GetValue().AsInt();

                    var picData = default(byte[]);
                    var beforePath = baseField["m_StreamData"]["path"].GetValue().AsString();

                    //try get texture data from stream data
                    if (!string.IsNullOrWhiteSpace(beforePath))
                    {
                        string searchPath = beforePath;
                        var offset = baseField["m_StreamData"]["offset"].GetValue().AsUInt();
                        var size = baseField["m_StreamData"]["size"].GetValue().AsUInt();

                        if (searchPath.StartsWith("archive:/"))
                            searchPath = searchPath.Substring("archive:/".Length);

                        searchPath = Path.GetFileName(searchPath);
                        var reader = assetBundleFile.file.reader;
                        var dirInf = assetBundleFile.file.bundleInf6.dirInf;

                        for (int i = 0; i < dirInf.Length; i++)
                        {
                            var info = dirInf[i];
                            if (info.name == searchPath)
                            {
                                reader.Position = assetBundleFile.file.bundleHeader6.GetFileDataOffset() + info.offset + offset;
                                picData = reader.ReadBytes((int)size);
                                break;
                            }
                        }
                    }

                    //try get texture data from image data field
                    if ((picData?.Length ?? 0) == 0)
                    {
                        var imageDataField = baseField["image data"];
                        var arr = imageDataField.GetValue().value.asByteArray;
                        picData = new byte[arr.size];
                        Array.Copy(arr.data, picData, arr.size);
                    }

                    if ((picData?.Length ?? 0) == 0)
                        continue;

                    try
                    {
                        if (!TextureImportExport.ExportPng(picData, outputPngFile, width, height, format))
                            return new(false, $"export .png file failed because TextureImportExport.ExportPng() return false.");
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Can't decode .ab image: {e.Message} filePath:{inputABFile} format:{format}");
                        return new(false, $"export .png file failed: {e.Message}");
                    }
                }

                return new(true);
            });
        }
    }
}
