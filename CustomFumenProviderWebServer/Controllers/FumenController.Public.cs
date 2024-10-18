using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Services.Jacket;
using CustomFumenProviderWebServer.Utils;
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
    [Route("fumen")]
    [RequestSizeLimit(50_000_000)]
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
                var result = await jacketService.GenerateAssetbundleJacket(jacketFilePath, false, 520, 520, assetsFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate jacket failed:" + result.Message);

                rename(ref jacketFilePath, $"{musicIdStr}_s.png");
                result = await jacketService.GenerateAssetbundleJacket(jacketFilePath, true, 220, 220, assetsFolder);
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

                var result = await audioService.GenerateAcbAwbFiles(audioFilePath, set.MusicId, set.Title, audioFolder);
                if (!result.IsSuccess)
                    return new(false, "Generate audio failed:" + result.Message);
            }
            catch (Exception e)
            {
                return new DeliverResultResponse(false, $"Generate audio throw exception:{e.Message}");
            }

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
                Directory.Delete(optTempFolder, true);
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
                foreach (var srcFilePath in Directory.GetFiles(generatedTempFolder))
                {
                    //move files
                    var dstFilePath = Path.Combine(storagePath, Path.GetFileName(srcFilePath));
                    System.IO.File.Copy(srcFilePath, dstFilePath, true);
                }

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
    }
}
