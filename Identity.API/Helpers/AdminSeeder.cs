using Identity.Database;
using Identity.Interfaces;
using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Helpers;

public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var passwordHelper = scope.ServiceProvider.GetRequiredService<IPasswordHelper>();

        if (await context.Users.AnyAsync(u => u.Role == "Admin"))
        {
            return;
        }

        var email = config["AdminSeed:Email"] ?? "admin@curaslot.local";
        var password = config["AdminSeed:Password"] ?? "Admin1234!";

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Administrator",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        admin.PasswordHash = passwordHelper.HashPassword(admin, password);

        context.Users.Add(admin);
        await context.SaveChangesAsync();
    }
}
