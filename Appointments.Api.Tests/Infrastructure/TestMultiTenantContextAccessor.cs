using Finbuckle.MultiTenant.Abstractions;

namespace Appointments.Api.Tests.Infrastructure;

public class TestMultiTenantContextAccessor : IMultiTenantContextAccessor
{
    public IMultiTenantContext MultiTenantContext { get; set; } = new TestMultiTenantContext();
}

public class TestMultiTenantContext : IMultiTenantContext
{
    public ITenantInfo? TenantInfo { get; init; } = new TenantInfo { Id = "test-tenant", Identifier = "test-tenant", Name = "Test" };
    public bool IsResolved => TenantInfo != null;
    public StrategyInfo? StrategyInfo { get; init; }
}
