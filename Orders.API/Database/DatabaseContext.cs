using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orders.Models;

namespace Orders.Database;

public class DatabaseContext : MultiTenantDbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
    
    public DatabaseContext(
        IMultiTenantContextAccessor multiTenantContextAccessor,
        DbContextOptions<DatabaseContext> options) : base(multiTenantContextAccessor, options)
    {
    }
    
}