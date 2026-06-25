using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Menu.Models;
using Microsoft.EntityFrameworkCore;

namespace Menu.Database;

public class DatabaseContext : MultiTenantDbContext
{
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<MenuSchedule> MenuSchedules { get; set; }

    public DatabaseContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<DatabaseContext> options) : base(multiTenantContextAccessor, options)
    {
    }
    
}