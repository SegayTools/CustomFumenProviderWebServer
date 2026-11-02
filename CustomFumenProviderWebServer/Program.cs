using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.CacheManager;
using CustomFumenProviderWebServer.Services.Editor;
using CustomFumenProviderWebServer.Services.FileCacheList;
using CustomFumenProviderWebServer.Services.Gmail;
using CustomFumenProviderWebServer.Utils;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace CustomFumenProviderWebServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.Configure<FormOptions>(options => FumenUploadLimits.ConfigureFormOptions(options));

            // Gmail API / Google OAuth。参数全部来自环境变量，未配置时相关接口返回 503，不影响现有功能。
            var gmailOptions = GmailOptions.FromEnvironment();
            builder.Services.AddSingleton(gmailOptions);
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHttpClient(GmailOptions.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            if (gmailOptions.IsConfigured)
            {
                Directory.CreateDirectory(gmailOptions.DataDirectory);
                builder.Services.AddDataProtection()
                    .SetApplicationName("CustomFumenProviderWebServer")
                    .PersistKeysToFileSystem(
                        new DirectoryInfo(Path.Combine(gmailOptions.DataDirectory, "keys")));
                builder.Services.AddSingleton<IGmailRefreshTokenStore, GmailTokenStore>();
                builder.Services.AddSingleton<IGmailAccountStore, GmailAccountStore>();
            }
            else
            {
                builder.Services.AddSingleton<IGmailRefreshTokenStore, DisabledGmailRefreshTokenStore>();
                builder.Services.AddSingleton<IGmailAccountStore, DisabledGmailAccountStore>();
            }
            builder.Services.AddSingleton<GoogleOAuthStateStore>();
            builder.Services.AddSingleton<IGmailAccessTokenProvider, GmailTokenProvider>();
            builder.Services.AddSingleton<GmailAuthorizationService>();
            builder.Services.AddSingleton<GmailEmailSender>();

            var fumenFolderPath = Environment.GetEnvironmentVariable("FumenDirectory");
            if (string.IsNullOrWhiteSpace(fumenFolderPath))
                throw new Exception("Environment variable \"FumenDirectory\" is empty.");
            Directory.CreateDirectory(fumenFolderPath);

            //setup db
            var dbConnectString = Environment.GetEnvironmentVariable("DBConnectString");
            var dbVersion = Environment.GetEnvironmentVariable("DBVersion");
            if (string.IsNullOrWhiteSpace(dbConnectString) || string.IsNullOrWhiteSpace(dbVersion))
                throw new Exception("Environment variables \"DBConnectString\" or \"DBVersion\" is empty");
            builder.Services.AddDbContextFactory<FumenDataDB>(options =>
                options.UseMySql(dbConnectString, new MariaDbServerVersion(Version.Parse(dbVersion))));

            //setup Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });

            builder.Services.AddDirectoryBrowser();

            //
            builder.Services.AddSingleton<JacketService>();
            builder.Services.AddSingleton<MusicXmlService>();
            builder.Services.AddSingleton<AudioService>();
            builder.Services.AddSingleton<FumenOperationLock>();
            builder.Services.AddSingleton<AdminAuthService>();
            builder.Services.AddSingleton<FumenStorageService>();
            builder.Services.AddSingleton<FumenChartService>();
            builder.Services.AddSingleton<FumenOptConsistencyService>();
            builder.Services.AddSingleton<FumenRemovalService>();
            builder.Services.AddSingleton<FumenStateService>();

            builder.Services.AddSingleton<IEditorService, EditorService>();
            builder.Services.AddSingleton<EditorUploadService>();
            builder.Services.AddHostedService<EditorServiceUpdater>();

            builder.Services.AddSingleton<IFileCacheListService, FileCacheListService>();
            builder.Services.AddHostedService<FileCacheListUpdater>();
            builder.Services.AddHostedService<FumenOptConsistencyHostedService>();

            //----------------------------------------------------------

            var app = builder.Build();

            var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
            if (gmailOptions.IsConfigured)
            {
                startupLogger.LogInformation(
                    "Gmail integration enabled. Sender: {SenderAddress}; redirect URI: {RedirectUri}; data directory: {DataDirectory}.",
                    gmailOptions.SenderAddress,
                    gmailOptions.RedirectUri,
                    gmailOptions.DataDirectory);
            }
            else
            {
                startupLogger.LogWarning(
                    "Gmail integration disabled. Missing or invalid environment variables: {MissingSettings}.",
                    string.Join(", ", gmailOptions.GetMissingSettings()));
            }

            //启动时读取并初始化管理员凭据(环境变量 ADMIN_PASSWORD 或默认哈希)
            app.Services.GetRequiredService<AdminAuthService>();

            //setup db migrations.
            await CheckDBMigrations<FumenDataDB>(app.Services);

            app.UseHttpsRedirection();

            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings.Remove(string.Empty);

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(fumenFolderPath),
                ContentTypeProvider = provider,
                ServeUnknownFileTypes = true,
                RequestPath = "/files",
            });

            app.UseDirectoryBrowser(new DirectoryBrowserOptions()
            {
                FileProvider = new PhysicalFileProvider(fumenFolderPath),
                RequestPath = "/files",
            });

            app.UseAuthorization();
            app.UseSwagger();
            app.UseSwaggerUI();
            app.MapControllers();

            app.Run();
        }


        private static async ValueTask CheckDBMigrations<T>(IServiceProvider provider) where T : DbContext
        {
            var loggerFactory = provider.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(typeof(T));

            using var scope = provider.CreateScope();
            using var db = scope.ServiceProvider.GetRequiredService<T>();
            /*
            if (db.Database.EnsureCreated())
                logger.LogInformation("Database has been create.");
            */
            var applieds = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            var pendings = (await db.Database.GetPendingMigrationsAsync()).ToArray();

            logger.LogInformation("Applied migrations:");
            foreach (var item in applieds)
                logger.LogInformation($" * {item}");
            logger.LogInformation("Pending migrations:");
            foreach (var item in pendings)
                logger.LogInformation($" * {item}");

            await db.Database.MigrateAsync();
        }
    }
}
