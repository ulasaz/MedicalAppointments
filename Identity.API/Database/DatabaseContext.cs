using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Identity.Database;

public class DatabaseContext : MultiTenantDbContext
{
    public DbSet<User> Users { get; set; }
    public DatabaseContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<DatabaseContext> options) : base(multiTenantContextAccessor, options)
    {
    }
}