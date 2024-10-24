using CustomFumenProviderWebServer.Models.Tables;
using Microsoft.EntityFrameworkCore;

namespace CustomFumenProviderWebServer.Databases
{
    public class FumenDataDB : DbContext
    {
        public DbSet<FumenSet> FumenSets { get; set; }
        public DbSet<FumenDifficult> FumenDifficults { get; set; }
        public DbSet<FumenOwner> FumenOwners { get; set; }

        public FumenDataDB(DbContextOptions<FumenDataDB> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FumenSet>()
                .HasKey(x => x.MusicId);

            modelBuilder.Entity<FumenDifficult>()
                .HasOne(fd => fd.FumenSet)
                .WithMany(fs => fs.FumenDifficults)
                .IsRequired()
                .HasForeignKey(fd => fd.MusicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<FumenDifficult>()
                .HasKey(fd => new { fd.MusicId, fd.DifficultIndex });

            modelBuilder.Entity<FumenOwner>()
                .HasKey(fd => new { fd.MusicId });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseLazyLoadingProxies(builder =>
            {
                builder.IgnoreNonVirtualNavigations();
            });
        }
    }
}
