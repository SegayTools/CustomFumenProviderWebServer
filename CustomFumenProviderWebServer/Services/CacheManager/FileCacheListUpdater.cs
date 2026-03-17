
using CustomFumenProviderWebServer.Services.CacheManager;
using System.Text.RegularExpressions;

namespace CustomFumenProviderWebServer.Services.FileCacheList
{
    public class FileCacheListUpdater : IHostedService
    {
        private static readonly Regex FumenMusicIdRegex = new(@"/fumen(\d+)/opt/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private CancellationTokenSource cancelTokenSource;
        private Task updateTask;
        private FileSystemWatcher fileSystemWatcher;
        private readonly ILogger<FileCacheListUpdater> logger;
        private readonly IFileCacheListService cacheListService;
        private readonly string fumenFolderPath;

        public FileCacheListUpdater(ILogger<FileCacheListUpdater> logger, IFileCacheListService cacheListService)
        {
            this.logger = logger;
            this.cacheListService = cacheListService;
            this.fumenFolderPath = cacheListService.GetFumenFolderPath();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancelTokenSource = new CancellationTokenSource();

            StartFileWatcher();
            updateTask = Task.Run(() => OnUpdate(cancelTokenSource.Token), cancellationToken);

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            cancelTokenSource?.Cancel();
            cancelTokenSource = null;

            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
                fileSystemWatcher.Created -= FileWatcherOnChanged;
                fileSystemWatcher.Changed -= FileWatcherOnChanged;
                fileSystemWatcher.Renamed -= FileWatcherOnRenamed;
                fileSystemWatcher.Dispose();
                fileSystemWatcher = null;
            }

            if (updateTask != null)
                await Task.WhenAny(updateTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        private void StartFileWatcher()
        {
            fileSystemWatcher = new FileSystemWatcher(fumenFolderPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                Filter = "*.*",
                EnableRaisingEvents = true
            };

            fileSystemWatcher.Created += FileWatcherOnChanged;
            fileSystemWatcher.Changed += FileWatcherOnChanged;
            fileSystemWatcher.Renamed += FileWatcherOnRenamed;
        }

        private void FileWatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            logger.LogInformation($"detected file change: {e.FullPath}");
            _ = OnFileChanged(e.FullPath);
        }

        private void FileWatcherOnRenamed(object sender, RenamedEventArgs e)
        {
            logger.LogInformation($"detected file renamed: {e.FullPath}");
            _ = OnFileChanged(e.FullPath);
        }

        private async Task OnFileChanged(string path)
        {
            try
            {
                var normalized = path.Replace('\\', '/');
                var match = FumenMusicIdRegex.Match(normalized);
                if (!match.Success)
                    return;

                if (!int.TryParse(match.Groups[1].Value, out var musicId))
                    return;

                logger.LogInformation("on file changed: {Path}, musicId: {MusicId}", path, musicId);
                await cacheListService.UpdateCacheFumenInfo(musicId);
            }
            catch (Exception e)
            {
                logger.LogError(e, "call OnFileChanged() failed, path: {Path}", path);
            }
        }

        private async Task OnUpdate(CancellationToken token)
        {
            await cacheListService.ForceRebuildAll();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await cacheListService.ScanChanges(token);
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"call OnUpdateInternal() failed: {e.Message}");
                }
            }
        }
    }
}
