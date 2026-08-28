using Identity.Database;
using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Helpers;

/// <summary>Runs once, before any other seeding: guarantees at least one medical center
/// exists and every pre-multitenancy Patient/Doctor row (TenantId still null from before
/// this column existed) gets attached to it, so nobody is orphaned once tenant scoping
/// goes live. The platform Admin is deliberately left with TenantId == null.</summary>
public static class DefaultTenantSeeder
{
    public const string DefaultSlug = "curaslot";

    /// <summary>Fixed across every service (not looked up) so Doctors.API and
    /// Appointments.API's own migrations can backfill their pre-multitenancy rows to the
    /// same tenant without a cross-service call during migration.</summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

        var defaultCenter = await context.MedicalCenters.FirstOrDefaultAsync(m => m.Slug == DefaultSlug);
        if (defaultCenter == null)
        {
            defaultCenter = new MedicalCenter
            {
                Id = DefaultTenantId,
                Name = "CuraSlot Medical Center",
                Slug = DefaultSlug,
                PrimaryColorHex = "#f43f5e",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.MedicalCenters.Add(defaultCenter);
            await context.SaveChangesAsync();
        }

        var orphaned = await context.Users
            .Where(u => u.TenantId == null && u.Role != "Admin")
            .ToListAsync();

        if (orphaned.Count > 0)
        {
            foreach (var user in orphaned)
            {
                user.TenantId = defaultCenter.Id;
            }
            await context.SaveChangesAsync();
        }
    }
}
