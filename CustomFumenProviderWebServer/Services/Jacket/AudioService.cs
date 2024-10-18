using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Utils;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using static CustomFumenProviderWebServer.Utils.ProcessExec;
using System.Xml.XPath;

namespace CustomFumenProviderWebServer.Services.Jacket
{
    public class AudioService
    {
        private readonly ILogger<AudioService> logger;
        private string binPath;

        public AudioService(ILogger<AudioService> logger)
        {
            this.logger = logger;

            var binFolder = "";
            var resourcePath = "";

            binFolder = Path.GetTempFileName().Replace(".", string.Empty) + "_AcbGeneratorFuck";

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                resourcePath = "CustomFumenProviderWebServer.Resources.Audio.linux.zip";
                binPath = Path.Combine(binFolder, "AcbGeneratorFuck.Console");
            }
            else
            {
                resourcePath = "CustomFumenProviderWebServer.Resources.Audio.win.zip";
                binPath = Path.Combine(binFolder, "AcbGeneratorFuck.Console.exe");
            }

            Directory.CreateDirectory(binFolder);

            using var zip = new ZipArchive(typeof(JacketService).Assembly.GetManifestResourceStream(resourcePath));
            logger.LogInformation($"extract {resourcePath} to {binFolder}");
            zip.ExtractToDirectory(binFolder, true);
            logger.LogInformation($"AcbGeneratorFuck bin file: {binPath}");

            if (Environment.OSVersion.Platform == PlatformID.Unix)
                File.SetUnixFileMode(binPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        private Task<ExecResult> Exec(params string[] args)
        {
            return ProcessExec.Exec(binPath, args);
        }

        public async Task<Result> GenerateAcbAwbFiles(string inputAudioFile, int musicId, string title, string outputFolder)
        {
            var musicIdStr = musicId.ToString().PadLeft(4, '0');
            var args = new[] {
                "--inputAudioFiles", inputAudioFile,
                "--inputAudioNamePrefixes", $"music{musicIdStr}",
                "--inputAudioOutputFolderPaths", outputFolder,
                "--gameType", "SDDT",
                "--keyCode", "0",
                "--noPause"
            };
            var execResult = await Exec(args);

            if (!(execResult.Output?.Contains("SUCCESS") ?? false))
                return new(false, execResult.Output);

            await GenerateMusicSourceXmlAsync(outputFolder, musicId, title);

            return new(true);
        }

        public async ValueTask GenerateMusicSourceXmlAsync(string outputFolder, int musicId, string title)
        {
            using var resStream = typeof(AudioService).Assembly.GetManifestResourceStream("CustomFumenProviderWebServer.Resources.MusicSource.xml");
            var musicSourceXml = await XDocument.LoadAsync(resStream, LoadOptions.None, default);

            var musicIdStr = musicId.ToString().PadLeft(4, '0');

            musicSourceXml.XPathSelectElement("//Name/str").Value = title;
            musicSourceXml.XPathSelectElement("//Name/id").Value = musicIdStr;

            musicSourceXml.XPathSelectElement("//acbFile/path").Value = $"music{musicIdStr}.acb";
            musicSourceXml.XPathSelectElement("//awbFile/path").Value = $"music{musicIdStr}.awb";

            musicSourceXml.XPathSelectElement("//dataName").Value = $"musicsource{musicIdStr}";

            var output = Path.Combine(outputFolder, "MusicSource.xml");
            using var fs = File.OpenWrite(output);
            using var writer = XmlWriter.Create(fs, new XmlWriterSettings()
            {
                Async = true,
                Encoding = Encoding.UTF8,
                Indent = true
            });
            await musicSourceXml.SaveAsync(writer, default);
        }
    }
}
