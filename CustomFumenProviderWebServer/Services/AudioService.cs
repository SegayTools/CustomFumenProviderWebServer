using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Utils;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using static CustomFumenProviderWebServer.Utils.ProcessExec;
using System.Xml.XPath;

namespace CustomFumenProviderWebServer.Services
{
    public class AudioService
    {
        private readonly ILogger<AudioService> logger;
        private string acbGeneratorBinPath;
        private string acb2wavBinPath;

        public AudioService(ILogger<AudioService> logger)
        {
            this.logger = logger;

            var binFolder = "";
            var resourcePath = "";

            binFolder = Path.GetTempFileName() + "_AcbGeneratorFuck";

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                resourcePath = "CustomFumenProviderWebServer.Resources.Audio.linux.zip";
                acbGeneratorBinPath = Path.Combine(binFolder, "AcbGeneratorFuck.Console");
                acb2wavBinPath = Path.Combine(binFolder, "acb2wavs");
            }
            else
            {
                resourcePath = "CustomFumenProviderWebServer.Resources.Audio.win.zip";
                acbGeneratorBinPath = Path.Combine(binFolder, "AcbGeneratorFuck.Console.exe");
                acb2wavBinPath = Path.Combine(binFolder, "acb2wavs.exe");
            }

            Directory.CreateDirectory(binFolder);

            using var zip = new ZipArchive(typeof(JacketService).Assembly.GetManifestResourceStream(resourcePath));
            logger.LogInformation($"extract {resourcePath} to {binFolder}");
            zip.ExtractToDirectory(binFolder, true);
            logger.LogInformation($"AcbGeneratorFuck bin file: {acbGeneratorBinPath}");
            logger.LogInformation($"Acb2wavs file: {acb2wavBinPath}");

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                File.SetUnixFileMode(acbGeneratorBinPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                File.SetUnixFileMode(acb2wavBinPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        private Task<ExecResult> Exec(params string[] args)
        {
            return ProcessExec.Exec(acbGeneratorBinPath, args);
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

        /*
        public async Task<Result> DumpWav(string acbFile, string outputWavFile)
        {
            var temp = Path.GetTempFileName();
            File.Delete(temp);
            Directory.CreateDirectory(temp);

            File.Copy(acbFile, Path.Combine(temp,"audio.acb"));
            File.Copy(acbFile, Path.Combine(temp, "audio.awb"));

            using var proc = Process.Start("acb2wavs.exe", audioFilePath);
            await proc.WaitForExitAsync();

            var audioFolder = Path.GetDirectoryName(audioFilePath);
            if (Directory.GetFiles(audioFolder, "dat_000000.wav", SearchOption.AllDirectories).FirstOrDefault() is string wavFile)
            {
                File.Move(wavFile, outputAudioFile, true);
                return;
            }
        }
        */
    }
}
