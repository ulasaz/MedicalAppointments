using Doctors.Models;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Doctors.Database;

public class DatabaseContext : MultiTenantDbContext
{
    public DbSet<DoctorProfile> DoctorProfiles { get; set; }
    public DbSet<MedicalService> MedicalServices { get; set; }

    public DatabaseContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<DatabaseContext> options) : base(multiTenantContextAccessor, options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DoctorProfile>()
            .HasIndex(d => d.UserId)
            .IsUnique();

        modelBuilder.Entity<MedicalService>()
            .HasIndex(s => s.DoctorProfileId);
    }
}