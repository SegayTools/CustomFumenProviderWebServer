using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Buffers;
using System.IO.Compression;
using System.Linq.Expressions;
using System.Text.Json;
using System.Xml.Linq;

namespace CustomFumenProviderWebServer.Controllers
{
    [ApiController]
    [Route("api")]
    [RequestSizeLimit(300_000_000)]
    public partial class FumenController : ControllerBase
    {
        private readonly ILogger<FumenController> logger;
        private readonly IDbContextFactory<FumenDataDB> fumenDataDBFactory;
        private readonly AudioService audioService;
        private readonly JacketService jacketService;
        private readonly MusicXmlService musicXmlService;
        private readonly string fumenFolderPath;
        private static readonly JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public FumenController(ILogger<FumenController> logger, IDbContextFactory<FumenDataDB> fumenDataDBFactory, AudioService audioService, JacketService jacketService, MusicXmlService musicXmlService)
        {
            this.logger = logger;
            this.fumenDataDBFactory = fumenDataDBFactory;
            this.audioService = audioService;
            this.jacketService = jacketService;
            this.musicXmlService = musicXmlService;
            this.fumenFolderPath = Environment.GetEnvironmentVariable("FumenDirectory");
        }

        /// <summary>
        /// 获取(已公开的)谱面列表
        /// </summary>
        /// <param name="pageIdx">页号 (从0开始)</param>
        /// <param name="countPerPage">每一页显示多少个谱面</param>
        /// <param name="genre">需要过滤显示的分类，null则表示不过滤</param>
        /// <param name="order">需要排序的字段，null表示不排序</param>
        /// <param name="minFilterLevel"></param>
        /// <param name="maxFilterLevel"></param>
        /// <returns></returns>
        [Route("list")]
        [HttpGet]
        public async Task<FumenQueryResponse> List(int pageIdx, int countPerPage, string genre = null, string order = null, float? minFilterLevel = null, float? maxFilterLevel = null)
        {
            var offset = pageIdx * countPerPage;

            using var db = await fumenDataDBFactory.CreateDbContextAsync();

            IQueryable<FumenSet> list = db.FumenSets.Where(x => x.PublishState == Models.PublishState.Published);

            if (!string.IsNullOrWhiteSpace(genre))
            {
                list = list.Where(x => x.GenreName == genre);
            }

            if (!string.IsNullOrWhiteSpace(order))
            {
                list = list.OrderBy(x => EF.Property<FumenSet>(x, order));
            }

            if (minFilterLevel is float minLevel)
            {
                list = list.Where(x => x.FumenDifficults.Any(d => d.Level >= minLevel));
            }

            if (maxFilterLevel is float maxLevel)
            {
                list = list.Where(x => x.FumenDifficults.Any(d => d.Level <= maxLevel));
            }

            var fumenSets = await list.Include(x => x.FumenDifficults).ToListAsync();

            var response = new FumenQueryResponse();

            response.FumenSets = fumenSets.Skip(offset).Take(countPerPage).ToList();
            response.QueryResultTotal = fumenSets.Count;

            return response;
        }

        /// <summary>
        /// 获取(正在审核的)谱面列表
        /// </summary>
        /// <param name="pageIdx"></param>
        /// <param name="countPerPage"></param>
        /// <returns></returns>
        [Route("listPending")]
        [HttpGet]
        public async Task<FumenQueryResponse> ListPending(int pageIdx, int countPerPage)
        {
            var offset = pageIdx * countPerPage;

            using var db = await fumenDataDBFactory.CreateDbContextAsync();

            IQueryable<FumenSet> list = db.FumenSets.Where(x => x.PublishState == Models.PublishState.Pending);

            var fumenSets = await list.Include(x => x.FumenDifficults).ToListAsync();

            var response = new FumenQueryResponse();

            response.FumenSets = fumenSets.Skip(offset).Take(countPerPage).ToList();
            response.QueryResultTotal = fumenSets.Count;

            return response;
        }

        /// <summary>
        /// 投稿自制谱, 并等待审核
        /// 注意: 上传Body限制50M大小
        /// </summary>
        /// <param name="jacketFormFile">谱面封面文件 (.png/assetbundle)</param>
        /// <param name="audioFormFile">谱面音频文件 (.wav/mp3)</param>
        /// <param name="musicXmlFormFile">谱面歌曲信息文件 (Music.xml)</param>
        /// <param name="bscOgkrFormFile">(可选)谱面Basic文件 (.ogkr)</param>
        /// <param name="advOgkrFormFile">(可选)谱面Advanced文件 (.ogkr)</param>
        /// <param name="expOgkrFormFile">(可选)谱面Expert文件 (.ogkr)</param>
        /// <param name="mstOgkrFormFile">(可选)谱面Master文件 (.ogkr)</param>
        /// <param name="lucOgkrFormFile">(可选)谱面Lunatic文件 (.ogkr)</param>
        /// <returns></returns>
        [Route("deliverFumen")]
        [HttpPost]
        public async Task<DeliverResultResponse> DeliverFumen(
            IFormFile jacketFormFile,
            IFormFile audioFormFile,
            IFormFile musicXmlFormFile,

            IFormFile bscOgkrFormFile,
            IFormFile advOgkrFormFile,
            IFormFile expOgkrFormFile,
            IFormFile mstOgkrFormFile,
            IFormFile lucOgkrFormFile
            )
        {
            async Task downloadToFile(string saveFilePath, IFormFile file)
            {
                if (file is null)
                    return;
                var formStream = file.OpenReadStream();
                using var fs = System.IO.File.OpenWrite(saveFilePath);
                await formStream.CopyToAsync(fs);
            }

            if (jacketFormFile is null)
                return new(false, "jacket file is not upload.");
            if (audioFormFile is null)
                return new(false, "audio file is not upload.");
            if (musicXmlFormFile is null)
                return new(false, "Music.xml file is not upload.");

            var tempFolder = TempPathUtils.GetNewTempFolder();
            var jacketFilePath = Path.Combine(tempFolder, $"jacket" + Path.GetExtension(jacketFormFile.FileName));
            var audioFilePath = Path.Combine(tempFolder, $"audio" + Path.GetExtension(audioFormFile.FileName));
            var bscOgkrFilePath = Path.Combine(tempFolder, $"fumen1.ogkr");
            var advOgkrFilePath = Path.Combine(tempFolder, $"fumen2.ogkr");
            var expOgkrFilePath = Path.Combine(tempFolder, $"fumen3.ogkr");
            var mstOgkrFilePath = Path.Combine(tempFolder, $"fumen4.ogkr");
            var lucOgkrFilePath = Path.Combine(tempFolder, $"fumen5.ogkr");
            var xmlFilePath = Path.Combine(tempFolder, $"music.xml");

            var generatedTempFolder = Path.Combine(tempFolder, "generated");
            Directory.CreateDirectory(generatedTempFolder);

            var optTempFolder = Path.Combine(generatedTempFolder, "opt");
            Directory.CreateDirectory(optTempFolder);

            await Task.WhenAll([
                downloadToFile(jacketFilePath,jacketFormFile),
                downloadToFile(audioFilePath,audioFormFile),
                downloadToFile(bscOgkrFilePath,bscOgkrFormFile),
                downloadToFile(advOgkrFilePath,advOgkrFormFile),
                downloadToFile(expOgkrFilePath,expOgkrFormFile),
                downloadToFile(mstOgkrFilePath,mstOgkrFormFile),
                downloadToFile(lucOgkrFilePath,lucOgkrFormFile),
                downloadToFile(xmlFilePath,musicXmlFormFile),
                ]);

            //step1: generate FumenSet
            FumenSet set;
            try
            {
                var result = await musicXmlService.GenerateFumenSet(xmlFilePath, bscOgkrFilePath, advOgkrFilePath, expOgkrFilePath, mstOgkrFilePath, lucOgkrFilePath);
                if (!result.IsSuccess)
                    return new(false, result.Message);
                set = result.Data;
            }
            catch (Exception e)
            {
                return new DeliverResultResponse(false, $"Generate FumenSet throw exception:{e.Message}");
            }

            //step1.5: re-alloc new musicId
            var newMusicId = set.MusicId + 20000;
            set.MusicId = newMusicId;

            var musicIdStr = set.MusicId.ToString().PadLeft(4, '0');

            //step2: generate Jacket
            try
            {
                void rename(ref string filePath, string newName)
                {
                    var newFilePath = Path.Combine(Path.GetDirectoryName(filePath), newName);
                    System.IO.File.Move(filePath, newFilePath);
                    filePath = newFilePath;
                }

                if (await FileUtils.CheckFileSignature(jacketFilePath, "UnityFS"))
                {
                    //dump as png file
                    rename(ref jacketFilePath, $"jacket.ab");
                    var outputPngFile = Path.Combine(Path.GetDirectoryName(jacketFilePath), "output.png");
                    var dumpResult = await jacketService.DumpPngFromAssetbundle(jacketFilePath, outputPngFile);
                    if (!dumpResult.IsSuccess)
                        return new(false, $"convert .ab to .png failed: {dumpResult.Message}");
                    jacketFilePath = outputPngFile;
                }

                var assetsFolder = Path.Combine(optTempFolder, "assets");
                Directory.CreateDirectory(assetsFolder);

                rename(ref jacketFilePath, $"{musicIdStr}.png");
                var result = await jacketService.GenerateAssetbundleJacket(jacketFilePath, 520, 520, assetsFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate jacket failed:" + result.Message);

                rename(ref jacketFilePath, $"{musicIdStr}_s.png");
                result = await jacketService.GenerateAssetbundleJacket(jacketFilePath, 220, 220, assetsFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate jacket failed:" + result.Message);
            }
            catch (Exception e)
            {
                return new DeliverResultResponse(false, $"Generate jacket throw exception:{e.Message}");
            }

            //step3: generate Audio
            try
            {
                var audioFolder = Path.Combine(optTempFolder, "musicsource", $"musicsource{musicIdStr}");
                Directory.CreateDirectory(audioFolder);

                if (audioFilePath.EndsWith(".zip"))
                {
                    using var fs = System.IO.File.OpenRead(audioFilePath);
                    using var zip = new ZipArchive(fs, ZipArchiveMode.Read, true);
                    var unpackFolder = Path.Combine(tempFolder, $"unpack.{Path.GetFileName(audioFilePath)}");
                    Directory.CreateDirectory(unpackFolder);
                    zip.ExtractToDirectory(unpackFolder);

                    var zipFiles = Directory.GetFiles(unpackFolder);

                    if (zipFiles.Length == 0)
                        return new(false, "audio.zip not contains audio file."); //fuck

                    //if audio.zip contains .acb(and .awb), copy them to opt/musicsource folder directly.
                    if (zipFiles.FirstOrDefault(x => x.EndsWith(".acb")) is string acbFile)
                    {
                        var dstAcbFile = Path.Combine(audioFolder, $"music{musicIdStr}.acb");
                        System.IO.File.Copy(acbFile, dstAcbFile);

                        var dstAwbFile = Path.Combine(audioFolder, $"music{musicIdStr}.awb");
                        var awbFile = Path.ChangeExtension(acbFile, ".awb");
                        if (System.IO.File.Exists(awbFile))
                            System.IO.File.Copy(awbFile, dstAwbFile);

                        await audioService.GenerateMusicSourceXmlAsync(audioFolder, set.MusicId, set.Title);
                        goto AUDIO_GENERATOR_SKIP;
                    }

                    //maybe audio.zip contains audio.wav/mp3?
                    audioFilePath = zipFiles.FirstOrDefault();
                }

                var result = await audioService.GenerateAcbAwbFiles(audioFilePath, set.MusicId, set.Title, audioFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate audio failed:" + result.Message);
            }
            catch (Exception e)
            {
                return new DeliverResultResponse(false, $"Generate audio throw exception:{e.Message}");
            }
        AUDIO_GENERATOR_SKIP:

            //step4: copy files.
            var fumenFolder = Path.Combine(optTempFolder, "music", $"music{musicIdStr}");
            Directory.CreateDirectory(fumenFolder);
            try
            {
                void CopyOgkrFile(string ogkrFile, int diffIdx)
                {
                    if (!System.IO.File.Exists(ogkrFile))
                        return;

                    var fileName = $"{musicIdStr}_0{diffIdx}.ogkr";
                    var outputFumenFile = Path.Combine(fumenFolder, fileName);

                    System.IO.File.Copy(ogkrFile, outputFumenFile, true);
                }

                CopyOgkrFile(bscOgkrFilePath, 0);
                CopyOgkrFile(advOgkrFilePath, 1);
                CopyOgkrFile(expOgkrFilePath, 2);
                CopyOgkrFile(mstOgkrFilePath, 3);
                CopyOgkrFile(lucOgkrFilePath, 4);
            }
            catch (Exception e)
            {
                return new DeliverResultResponse(false, $"Copy fumen files throw exception:{e.Message}");
            }

            //step5: generate Music.xml
            try
            {
                var outputXmlFilePath = Path.Combine(fumenFolder, "Music.xml");
                await musicXmlService.GenerateMusicXml(set, outputXmlFilePath);
            }
            catch (Exception e)
            {
                return new DeliverResultResponse(false, $"Copy fumen files throw exception:{e.Message}");
            }

            //todo step6: add Readme.md and RegisterFumenJackets.exe

            //step7: pack opt as .zip
            var zipFile = Path.Combine(generatedTempFolder, $"fumen{musicIdStr}.zip");
            await Task.Run(() =>
            {
                using var fs = System.IO.File.OpenWrite(zipFile);
                ZipFile.CreateFromDirectory(optTempFolder, fs, CompressionLevel.SmallestSize, false);
            });

            //step8: generate info.json
            {
                var infoJsonFilePath = Path.Combine(generatedTempFolder, $"info.json");
                using var fs = System.IO.File.OpenWrite(infoJsonFilePath);
                await JsonSerializer.SerializeAsync(fs, set, jsonSerializerOptions);
            }

            //step8: move .png file
            {
                var outputJacketFilePath = Path.Combine(generatedTempFolder, $"jacket.png");
                System.IO.File.Move(jacketFilePath, outputJacketFilePath, true);
            }

            //step9: register in database
            set.PublishState = Models.PublishState.Pending;
            set.UpdateTime = DateTime.Now;

            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            using var trans = await db.Database.BeginTransactionAsync();

            try
            {

                var isUpdate = false;
                if ((await db.FumenSets.FindAsync(set.MusicId)) is FumenSet cSet)
                {
                    isUpdate = true;
                    db.Remove(cSet);
                    await db.SaveChangesAsync();
                }

                db.Add(set);
                await db.SaveChangesAsync();

                //step10: move generated files to storage.
                var storagePath = Path.Combine(fumenFolderPath, $"fumen{musicIdStr}");
                Directory.CreateDirectory(storagePath);
                if (isUpdate)
                {
                    //remove old files
                    foreach (var deleteFilePath in Directory.GetFiles(storagePath))
                        System.IO.File.Delete(deleteFilePath);
                }

                FileUtils.CopyDirectory(generatedTempFolder, storagePath);

                await trans.CommitAsync();
                return new(true, "register successfully, waiting for pending", set);
            }
            catch (Exception e)
            {
                await trans.RollbackAsync();
                return new(false, $"Register fumen data throw exception: {e.Message}");
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }
        }

        async ValueTask<Result> ProcessJacketToFile(string inputFile, string outputFolder, int musicId)
        {
            var musicIdStr = musicId.ToString().PadLeft(4, '0');
            var tempFolder = TempPathUtils.GetNewTempFolder();

            try
            {
                if (await FileUtils.CheckFileSignature(inputFile, "UnityFS"))
                {
                    //dump as png file
                    var inputABFile = Path.Combine(tempFolder, $"jacket.ab");
                    System.IO.File.Copy(inputFile, inputABFile);
                    var outputPngFile = Path.Combine(tempFolder, $"output.png");

                    var dumpResult = await jacketService.DumpPngFromAssetbundle(inputABFile, outputPngFile);
                    if (!dumpResult.IsSuccess)
                        return new(false, $"convert .ab to .png failed: {dumpResult.Message}");
                    inputFile = outputPngFile;
                }

                var assetsFolder = Path.Combine(outputFolder, "assets");
                Directory.CreateDirectory(assetsFolder);

                var pngFile = Path.Combine(tempFolder, $"{musicIdStr}.png");
                var pngFileSmall = Path.Combine(tempFolder, $"{musicIdStr}_s.png");
                System.IO.File.Copy(inputFile, pngFile, true);
                System.IO.File.Copy(inputFile, pngFileSmall, true);

                var result = await jacketService.GenerateAssetbundleJacket(pngFile, 520, 520, assetsFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate jacket failed:" + result.Message);

                result = await jacketService.GenerateAssetbundleJacket(pngFileSmall, 220, 220, assetsFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate jacket small failed:" + result.Message);

                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"Generate jacket throw exception:{e.Message}");
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }
        }

        ValueTask<Result> ProcessFumenToFile(string ogkrFile, string outputFolder, int musicId, int diffIdx)
        {
            var musicIdStr = musicId.ToString().PadLeft(4, '0');
            var diffIdxStr = musicId.ToString().PadLeft(2, '0');
            var outputFile = Path.Combine(outputFolder, $"{musicIdStr}_{diffIdxStr}.ogkr");

            try
            {
                System.IO.File.Copy(ogkrFile, outputFile, true);
                return ValueTask.FromResult(new Result(true));
            }
            catch (Exception e)
            {
                return ValueTask.FromResult(new Result(false, $"Copy ogkr file throw exception:{e.Message}"));
            }
        }

        async ValueTask<Result> ProcessAudioToFile(string inputFile, string outputFolder, int musicId, string title)
        {
            var musicIdStr = musicId.ToString().PadLeft(4, '0');
            var tempFolder = TempPathUtils.GetNewTempFolder();

            try
            {
                var audioFolder = Path.Combine(outputFolder, "musicsource", $"musicsource{musicIdStr}");
                Directory.CreateDirectory(audioFolder);

                if (inputFile.EndsWith(".zip"))
                {
                    using var fs = System.IO.File.OpenRead(inputFile);
                    using var zip = new ZipArchive(fs, ZipArchiveMode.Read, true);
                    var unpackFolder = Path.Combine(tempFolder, $"unpack.{Path.GetFileName(inputFile)}");
                    Directory.CreateDirectory(unpackFolder);
                    zip.ExtractToDirectory(unpackFolder);

                    var zipFiles = Directory.GetFiles(unpackFolder);

                    if (zipFiles.Length == 0)
                        return new(false, "audio.zip not contains audio file."); //fuck

                    //if audio.zip contains .acb(and .awb), copy them to opt/musicsource folder directly.
                    if (zipFiles.FirstOrDefault(x => x.EndsWith(".acb")) is string acbFile)
                    {
                        var dstAcbFile = Path.Combine(audioFolder, $"music{musicIdStr}.acb");
                        System.IO.File.Copy(acbFile, dstAcbFile);

                        var dstAwbFile = Path.Combine(audioFolder, $"music{musicIdStr}.awb");
                        var awbFile = Path.ChangeExtension(acbFile, ".awb");
                        if (System.IO.File.Exists(awbFile))
                            System.IO.File.Copy(awbFile, dstAwbFile);

                        await audioService.GenerateMusicSourceXmlAsync(audioFolder, musicId, title);
                        return new(true);
                    }

                    //maybe audio.zip contains audio.wav/mp3?
                    inputFile = zipFiles.FirstOrDefault();
                }

                var result = await audioService.GenerateAcbAwbFiles(inputFile, musicId, title, audioFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate audio failed:" + result.Message);
                return new(true);
            }
            catch (Exception e)
            {
                return new(false, $"Generate audio throw exception:{e.Message}");
            }
            finally
            {
                Directory.Delete(tempFolder, true);
            }
        }
    }
}
