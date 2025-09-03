using Microsoft.EntityFrameworkCore;
using PersonaWatch.WebApi.Entities;

namespace PersonaWatch.WebApi.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<NewsContent> NewsContents => Set<NewsContent>();

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override int SaveChanges()
        {
            var entries = ChangeTracker.Entries<BaseEntity>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedDate = DateTime.UtcNow;
                    entry.Entity.CreatedUserName = "system";
                }

                if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedDate = DateTime.UtcNow;
                    entry.Entity.UpdatedUserName = "system";
                }
            }

            return base.SaveChanges();
        }
    }
}
