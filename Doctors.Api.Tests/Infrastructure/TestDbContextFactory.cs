using Doctors.Database;
using Microsoft.EntityFrameworkCore;

namespace Doctors.Api.Tests.Infrastructure;

public static class TestDbContextFactory
{
    public static DatabaseContext Create()
    {
        var options = new DbContextOptionsBuilder<DatabaseContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DatabaseContext(new TestMultiTenantContextAccessor(), options);
    }
}
