using Appointments.Models;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Appointments.Database;

public class DatabaseContext : MultiTenantDbContext
{
    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<AvailabilitySlot> AvailabilitySlots { get; set; }
    public DbSet<Review> Reviews { get; set; }

    public DatabaseContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<DatabaseContext> options) : base(multiTenantContextAccessor, options)
    {
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Appointment>()
            .HasIndex(a => new { a.DoctorId, a.StartTime })
            .IsUnique();

        modelBuilder.Entity<AvailabilitySlot>()
            .HasIndex(s => new { s.DoctorId, s.Date });

        modelBuilder.Entity<Review>()
            .HasIndex(r => r.AppointmentId)
            .IsUnique();

        modelBuilder.Entity<Review>()
            .HasIndex(r => r.DoctorId);
    }

}