using Microsoft.Extensions.Hosting;

namespace CustomFumenProviderWebServer.Services
{
    /// <summary>
    /// 启动时按 EnableCreatorRewrite 检查并修复所有 fumen{musicId} 的 opt 状态：
    /// 开启时确保 opt 的 Music.xml 定数为 0、谱面带自制谱标记；
    /// 关闭时把已处理的内容恢复为上传原文与真实定数。
    /// </summary>
    public sealed class FumenOptConsistencyHostedService : IHostedService
    {
        private readonly FumenOptConsistencyService consistencyService;
        private readonly ILogger<FumenOptConsistencyHostedService> logger;

        public FumenOptConsistencyHostedService(
            FumenOptConsistencyService consistencyService,
            ILogger<FumenOptConsistencyHostedService> logger)
        {
            this.consistencyService = consistencyService;
            this.logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var result = await consistencyService.EnsureAllAsync(cancellationToken);
                if (result.IsSuccess)
                    logger.LogInformation("opt consistency check done: {Message}", result.Message);
                else
                    logger.LogWarning("opt consistency check finished with problems: {Message}", result.Message);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "opt consistency check failed");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
