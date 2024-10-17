using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomFumenProviderWebServer.Databases
{
    public class FumenDataDesignTimeDbContextFactory : IDesignTimeDbContextFactory<FumenDataDB>
    {
        public FumenDataDB CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FumenDataDB>();
            optionsBuilder.UseMySql("Server=localhost;Port=13306;Database=fumen;Uid=root;Pwd=q6523230;", new MySqlServerVersion(Version.Parse("8.0.32")));

            return new FumenDataDB(optionsBuilder.Options);
        }
    }
}
