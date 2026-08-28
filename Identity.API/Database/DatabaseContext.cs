using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Database;

public class DatabaseContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<MedicalCenter> MedicalCenters { get; set; }

    public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MedicalCenter>()
            .HasIndex(m => m.Slug)
            .IsUnique();
    }
}