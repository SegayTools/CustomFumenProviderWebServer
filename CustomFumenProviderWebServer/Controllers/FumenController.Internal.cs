using CustomFumenProviderWebServer.Models.Responses;
using CustomFumenProviderWebServer.Models.Tables;
using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CustomFumenProviderWebServer.Controllers
{
    public partial class FumenController : ControllerBase
    {
        /// <summary>
        /// 添加/更新 谱面信息
        /// </summary>
        /// <remarks>
        /// 上传的内容将完整地替代原有的内容
        /// </remarks>
        /// <param name="set">要上传的谱面信息</param>
        /// <returns></returns>
        [HttpPost]
        [Route("update")]
        public async Task<FumenUploadResponse> Upload([FromForm] string password, IFormFile packZip)
        {
            if (!CheckPermission(password))
            {
                HttpContext.Response.StatusCode = 403;
                return new(false, "no permission");
            }


            var zipTempFolder = Path.GetTempFileName();
            System.IO.File.Delete(zipTempFolder);
            Directory.CreateDirectory(zipTempFolder);

            try
            {
                using var ms = new MemoryStream();
                await packZip.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);

                using var zip = new ZipArchive(ms, ZipArchiveMode.Read, false);
                zip.ExtractToDirectory(zipTempFolder);
            }
            catch (Exception e)
            {
                return new(false, $"http body is not .zip file: {e.Message}");
            }

            return await ProcessZipFolder(zipTempFolder);
        }

        private async Task<FumenUploadResponse> ProcessZipFolder(string zipTempFolder)
        {
            logger.LogInformation($"zipTempFolder: {zipTempFolder}");

            var infoFilePath = Path.Combine(zipTempFolder, "info.json");
            if (!System.IO.File.Exists(infoFilePath))
                return new(false, "info.json not found");

            var set = JsonSerializer.Deserialize<FumenSet>(await System.IO.File.ReadAllTextAsync(infoFilePath));

            //check
            if (set.MusicId is > 10000 or <= 0)
                return new(false, "MusicId不符合格式");
            if (set.FumenDifficults.Count == 0)
                return new(false, "Diff列表为空");

            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            using var trans = await db.Database.BeginTransactionAsync();

            try
            {
                if ((await db.FumenSets.FindAsync(set.MusicId)) is FumenSet cSet)
                {
                    db.Remove(cSet);
                    await db.SaveChangesAsync();
                }

                db.Add(set);

                await db.SaveChangesAsync();
                await trans.CommitAsync();
                logger.LogInformation($"Updated info.json into database.");
            }
            catch (Exception e)
            {
                await trans.RollbackAsync();
                return new(false, e.Message);
            }

            var outputFolder = Path.Combine(fumenFolderPath, $"fumen{set.MusicId.ToString().PadLeft(4, '0')}");
            Directory.CreateDirectory(outputFolder);
            //copy files.
            foreach (var srcFilePath in Directory.GetFiles(zipTempFolder))
            {
                var fileName = Path.GetFileName(srcFilePath);
                var dstFilePath = Path.Combine(outputFolder, fileName);

                System.IO.File.Copy(srcFilePath, dstFilePath, true);
            }
            logger.LogInformation($"Copied files to folder{outputFolder}");

            Directory.Delete(zipTempFolder, true);
            logger.LogInformation($"Deleted temp files.");

            return new(true, string.Empty, set);
        }

        /// <summary>
        /// 删除 谱面信息
        /// </summary>
        /// <param name="musicId">要删除谱面信息的musicId</param>
        /// <returns></returns>
        [HttpDelete]
        [Route("remove")]
        public async Task<FumenUploadResponse> Remove([FromForm] int musicId, [FromForm] string password)
        {
            if (!CheckPermission(password))
            {
                HttpContext.Response.StatusCode = 403;
                return new(false, "no permission");
            }

            using var db = await fumenDataDBFactory.CreateDbContextAsync();
            using var trans = await db.Database.BeginTransactionAsync();

            try
            {
                if ((await db.FumenSets.FindAsync(musicId)) is FumenSet cSet)
                {
                    db.Remove(cSet);
                    await db.SaveChangesAsync();
                    await trans.CommitAsync();
                    return new(true);
                }

                return new(false, "fumen not found");
            }
            catch (Exception e)
            {
                await trans.RollbackAsync();
                return new(false, e.Message);
            }
        }

        private SHA256 sha256 = SHA256.Create();

        private bool CheckPermission(string password)
        {
            return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)))
                == "D7969812349992B265133AA1AA39BB82A7D5C9A3294C9ABDB337204C5CDE580F";
        }
    }
}
