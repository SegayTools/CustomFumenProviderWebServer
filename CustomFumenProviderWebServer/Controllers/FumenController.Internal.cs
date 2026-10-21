using CustomFumenProviderWebServer.Models.Responses;
using Microsoft.AspNetCore.Mvc;

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

            using var lockScope = await fumenOperationLock.AcquireAsync(musicId);

            var result = await fumenStateService.PublishAsync(musicId);
            return new(result.IsSuccess, result.Message);
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

            using var lockScope = await fumenOperationLock.AcquireAsync(musicId);

            var result = await fumenRemovalService.RemoveAsync(musicId);
            if (result.IsSuccess)
                fileCacheListService.CacheFumenMap.TryRemove(musicId, out _);

            return new(result.IsSuccess, result.Message);
        }

    }
}
