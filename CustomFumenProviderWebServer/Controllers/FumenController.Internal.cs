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
        /// <param name="password">管理员审核密码</param>
        /// <returns>公开操作结果</returns>
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
                    cSet.UpdateTime = DateTime.Now;

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
        /// <param name="password">管理员审核密码</param>
        /// <returns>删除操作结果</returns>
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
                var any = false;
                void delete<T>(List<T> removes)
                {
                    any = any || removes.Count > 0;
                    foreach (var cFumen in removes)
                        db.Remove(cFumen);
                }

                delete(await db.FumenSets.Where(x => x.MusicId == musicId).ToListAsync());
                delete(await db.FumenDifficults.Where(x => x.MusicId == musicId).ToListAsync());
                delete(await db.FumenOwners.Where(x => x.MusicId == musicId).ToListAsync());

                await db.SaveChangesAsync();
                await trans.CommitAsync();

                return new(any);
            }
            catch (Exception e)
            {
                await trans.RollbackAsync();
                return new(false, e.Message);
            }
        }

    }
}
