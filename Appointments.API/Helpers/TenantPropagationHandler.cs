using Finbuckle.MultiTenant.Abstractions;

namespace Appointments.Helpers;

/// <summary>Forwards the current request's resolved tenant to downstream service-to-service
/// HTTP calls (Appointments.API -> Doctors.API for doctor/service lookups) via X-Tenant-Id.
/// Those calls don't carry the caller's JWT, so without this the receiving side would resolve
/// no tenant and silently return empty results behind its own Finbuckle query filter.</summary>
public class TenantPropagationHandler : DelegatingHandler
{
    private readonly IMultiTenantContextAccessor<TenantInfo> _accessor;

    public TenantPropagationHandler(IMultiTenantContextAccessor<TenantInfo> accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tenantId = _accessor.MultiTenantContext?.TenantInfo?.Id;
        if (!string.IsNullOrEmpty(tenantId))
        {
            request.Headers.Remove("X-Tenant-Id");
            request.Headers.Add("X-Tenant-Id", tenantId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
