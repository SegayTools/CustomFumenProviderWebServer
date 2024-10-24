using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Utils;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using static CustomFumenProviderWebServer.Utils.ProcessExec;

namespace CustomFumenProviderWebServer.Services
{
    public class MusicXmlService
    {
        private readonly ILogger<MusicXmlService> logger;
        private static Regex BpmRegex = new Regex(@"BPM_DEF\s*([\d\.]+)");
        private static Regex CreatorRegex = new Regex(@"CREATOR\s*(.+)");

        public MusicXmlService(ILogger<MusicXmlService> logger)
        {
            this.logger = logger;
        }

        public async Task<Result<FumenSet>> GenerateFumenSet(
            string xmlFile,
            string bscOgkrFile,
            string advOgkrFile,
            string expOgkrFile,
            string mstOgkrFile,
            string lucOgkrFile)
        {
            using var fs = File.OpenRead(xmlFile);
            var musicXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

            #region Xml Reading

            string GetString(string name)
            {
                return GetPathValue<string>(name, "str");
            }

            int GetId(string name)
            {
                return GetPathValue<int>(name, "id");
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            T GetPathValue<T>(params string[] names)
            {
                var expr = $"//{string.Join("/", names.Select(x => $"{x}[1]"))}";
                var element = musicXml.XPathSelectElement(expr);
                if (element?.Value is string strValue)
                {
                    var obj = TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(strValue);
                    if (obj is T t)
                        return t;
                }
                return default;
            }

            #endregion

            var set = new FumenSet()
            {
                MusicId = GetId("Name"),
                Title = GetString("Name"),
                Artist = GetString("ArtistName"),
                GenreId = GetId("Genre"),
                GenreName = GetString("Genre"),
                BossCardId = GetId("BossCard"),
                BossCardName = GetString("BossCard"),
                MusicRightsId = GetId("MusicRightsName"),
                MusicRightsName = GetString("MusicRightsName"),
                StageId = GetId("StageID"),
                StageName = GetString("StageID"),
                VersionId = GetId("VersionID"),
                VersionName = GetString("VersionID"),
                SortOrder = GetPathValue<int>("SortOrder"),
                BossLevel = GetPathValue<int>("BossLevel"),
                BossLockHpCoef = GetPathValue<int>("BossLockHpCoef"),
                BossVoiceNo = GetPathValue<int>("BossVoiceNo"),
                WaveAttribute = GetPathValue<string>("WaveAttribute", "AttributeType"),
            };

            //process diff
            foreach ((int idx, string fumenFilePath) in new[] {
                (1, bscOgkrFile),
                (2, advOgkrFile),
                (3, expOgkrFile),
                (4, mstOgkrFile),
                (5, lucOgkrFile),
            })
            {
                var fumenDataElement = musicXml.XPathSelectElement($"/MusicData/FumenData/FumenData[{idx}]");

                var fumenConstIntegerPart = fumenDataElement.Element("FumenConstIntegerPart").Value;
                var fumenConstFractionalPart = fumenDataElement.Element("FumenConstFractionalPart").Value;
                var fumenFileName = fumenDataElement.Element("FumenFile").Element("path")?.Value;

                if (!File.Exists(fumenFilePath))
                    continue;

                var diff = new FumenDifficult();
                diff.DifficultIndex = idx - 1;
                diff.MusicId = set.MusicId;
                diff.Level = (int.TryParse(fumenConstIntegerPart, out var d1) ? d1 : 0) + (int.TryParse(fumenConstFractionalPart, out var d2) ? d2 : 0) / 100.0f;

                var result = await ParseFumenFileInfo(diff, fumenFilePath);
                if (!result.IsSuccess)
                    return new(false, result.Message);

                set.FumenDifficults.Add(diff);
            }

            return new(true, string.Empty, set);
        }

        private static XElement GetNode(XNode document, params string[] fieldPaths)
        {
            try
            {
                var selectExpr = $"//{string.Join("/", fieldPaths.Select(x => $"{x}[1]"))}";
                return document.XPathSelectElement(selectExpr);
            }
            catch (Exception e)
            {
                return default;
            }
        }

        public async Task<Result> GenerateMusicXml(FumenSet set, string outputFilePath)
        {
            using var rs = typeof(MusicXmlService).Assembly.GetManifestResourceStream("CustomFumenProviderWebServer.Resources.Music.xml");
            var musicXml = XDocument.Load(rs);

            var idStr = set.MusicId.ToString().PadLeft(4, '0');
            GetNode(musicXml, "dataName").Value = $"music{idStr}";

            GetNode(musicXml, "Name", "id").Value = set.MusicId.ToString();
            GetNode(musicXml, "Name", "str").Value = set.Title;

            GetNode(musicXml, "ArtistName", "id").Value = set.MusicId.ToString();
            GetNode(musicXml, "ArtistName", "str").Value = set.Artist;

            GetNode(musicXml, "MusicRightsName", "id").Value = set.MusicRightsId.ToString();
            GetNode(musicXml, "MusicRightsName", "str").Value = set.MusicRightsId is 0 ? "-" : set.MusicRightsName;

            GetNode(musicXml, "MusicSourceName", "id").Value = set.MusicId.ToString();
            GetNode(musicXml, "MusicSourceName", "str").Value = set.Title;

            GetNode(musicXml, "Genre", "id").Value = set.GenreId.ToString();
            GetNode(musicXml, "Genre", "str").Value = set.GenreName;

            GetNode(musicXml, "BossCard", "id").Value = set.BossCardId.ToString();
            GetNode(musicXml, "BossCard", "str").Value = set.BossCardName;

            GetNode(musicXml, "VersionID", "id").Value = set.VersionId.ToString();
            GetNode(musicXml, "VersionID", "str").Value = set.VersionName;

            GetNode(musicXml, "StageID", "id").Value = set.StageId.ToString();
            GetNode(musicXml, "StageID", "str").Value = set.StageName;

            var fumenElements = musicXml.XPathSelectElements("//FumenData//FumenData").ToArray();
            for (int i = 0; i < 5; i++)
            {
                var fumenElement = fumenElements.ElementAtOrDefault(i);
                if (fumenElement is not null)
                {
                    var diff = set.FumenDifficults.FirstOrDefault(x => x.DifficultIndex == i);
                    if (diff is not null)
                    {
                        fumenElement.XPathSelectElement("./FumenConstIntegerPart").Value = ((int)diff.Level).ToString();
                        var fp = (int)Math.Round((diff.Level - (int)diff.Level) * 100 + 0.5);
                        fumenElement.XPathSelectElement("./FumenConstFractionalPart").Value = fp.ToString();
                        fumenElement.XPathSelectElement("./FumenFile/path").Value = $"{idStr}_0{i}.ogkr";
                    }
                    else
                    {
                        fumenElement.XPathSelectElement("./FumenFile/path").Value = string.Empty;
                        fumenElement.XPathSelectElement("./FumenConstFractionalPart").Value = "0";
                        fumenElement.XPathSelectElement("./FumenConstIntegerPart").Value = "0";
                    }
                }
            }

            GetNode(musicXml, "IsLunatic").Value = set.FumenDifficults.Any(x => x.DifficultIndex == 4).ToString().ToLower();

            GetNode(musicXml, "WaveAttribute", "AttributeType").Value = set.WaveAttribute.ToString();
            GetNode(musicXml, "BossLevel").Value = set.BossLevel.ToString();
            GetNode(musicXml, "SortOrder").Value = set.SortOrder.ToString();
            GetNode(musicXml, "NameForSort").Value = set.Title.ToUpper();
            GetNode(musicXml, "BossLockHpCoef").Value = set.BossLockHpCoef.ToString();
            GetNode(musicXml, "BossVoiceNo").Value = set.BossVoiceNo.ToString();

            using var fs = File.OpenWrite(outputFilePath);
            await musicXml.SaveAsync(fs, SaveOptions.None, default);

            return new Result(true);
        }

        private async Task<Result> ParseFumenFileInfo(FumenDifficult diff, string fumenFilePath)
        {
            try
            {
                using var fs = File.OpenRead(fumenFilePath);
                using var reader = new StreamReader(fs);

                var isBpmSetup = false;
                var isCreatorSetup = false;

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();

                    if (!isBpmSetup)
                    {
                        var match = BpmRegex.Match(line);
                        if (match.Success)
                        {
                            var bpm = float.Parse(match.Groups[1].Value);
                            isBpmSetup = true;

                            diff.Bpm = bpm;
                        }
                    }

                    if (!isCreatorSetup)
                    {
                        var match = CreatorRegex.Match(line);
                        if (match.Success)
                        {
                            var creator = match.Groups[1].Value;
                            isCreatorSetup = true;

                            diff.Creator = creator;
                        }
                    }

                    var cmd = line.Split('\t').FirstOrDefault();
                    switch (cmd.ToUpper())
                    {
                        case "HLD" or "CHD" or "XHD":
                            diff.HoldCount++;
                            break;
                        case "TAP" or "CTP" or "XTP":
                            diff.TapCount++;
                            break;
                        case "FLK" or "CFK":
                            diff.FlickCount++;
                            break;
                        case "BMS" or "OBS":
                            diff.BeamCount++;
                            break;
                        case "BLT":
                            diff.BulletCount++;
                            break;
                        case "BEL":
                            diff.BellCount++;
                            break;
                        case "SFL":
                            diff.SoflanCount++;
                            break;
                        case "BPM":
                            diff.BpmCount++;
                            break;
                        case "MET":
                            diff.MeterCount++;
                            break;
                    }
                }

                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"Parse {diff.DifficultName} fumen file failed:{e.Message}");
            }
        }
    }
}
