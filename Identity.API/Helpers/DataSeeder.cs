using Finbuckle.MultiTenant.Abstractions;
using Identity.Database;
using Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Identity.Helpers;

public class DataSeeder
{
    private readonly IServiceProvider _serviceProvider;

    public DataSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task SeedAsync(string tenantId)
    {
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<DatabaseContext>();
        
        if (!context.Users.IgnoreQueryFilters().Any(u => EF.Property<string>(u, "TenantId") == tenantId))
        {
            var passwordHasher = scope.ServiceProvider
                .GetRequiredService<IPasswordHasher<User>>();

            var cafeUser = new User
            {
                Id = Guid.NewGuid(),
                Email = $"cafe@{tenantId}.local",
                DisplayName = $"Cafe {tenantId}",
                Role = "Cafe",
                CreatedAt = DateTime.UtcNow
            };
            cafeUser.PasswordHash = passwordHasher.HashPassword(cafeUser, "Admin1234!");
            
            await context.Database.ExecuteSqlRawAsync
            ($@"
            INSERT INTO ""Users"" 
            (""Id"", ""Email"", ""PasswordHash"", ""DisplayName"", ""Role"", ""CreatedAt"", ""TenantId"")
            VALUES 
            ('{cafeUser.Id}', '{cafeUser.Email}', '{cafeUser.PasswordHash}', 
             '{cafeUser.DisplayName}', '{cafeUser.Role}', NOW(), '{tenantId}')
        ");
        }
    }

}