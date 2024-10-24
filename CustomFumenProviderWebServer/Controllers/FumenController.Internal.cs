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
        /// 审核通过,公开谱面
        /// </summary>
        /// <param name="musicId">要公开谱面的musicId</param>
        /// <returns></returns>
        [HttpPost]
        [Route("publishFumen")]
        public async Task<FumenUploadResponse> PublishFumen([FromForm] int musicId, [FromForm] string password)
        {
            if (!await VerifyPermission(password))
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
                    cSet.PublishState = Models.PublishState.Published;

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

        /// <summary>
        /// 删除 谱面
        /// </summary>
        /// <param name="musicId">要删除谱面的musicId</param>
        /// <returns></returns>
        [HttpDelete]
        [Route("remove")]
        public async Task<FumenUploadResponse> Remove([FromForm] int musicId, [FromForm] string password)
        {
            if (!await VerifyPermission(password))
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

    }
}
