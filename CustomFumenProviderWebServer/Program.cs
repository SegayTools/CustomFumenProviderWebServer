using CustomFumenProviderWebServer.Databases;
using CustomFumenProviderWebServer.Services;
using CustomFumenProviderWebServer.Services.Editor;
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

            builder.Services.AddSingleton<IEditorService, EditorService>();
            builder.Services.AddHostedService<EditorServiceUpdater>();

            //----------------------------------------------------------

            var app = builder.Build();

            //setup db migrations.
            await CheckDBMigrations<FumenDataDB>(app.Services);

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(fumenFolderPath),
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
