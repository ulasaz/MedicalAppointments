using Finbuckle.MultiTenant.Abstractions;

namespace Doctors.Models;

[MultiTenant]
public class MedicalService
{
    public Guid Id { get; set; }
    public Guid DoctorProfileId { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public List<VisitType> AllowedVisitTypes { get; set; } = new() { VisitType.Stationary, VisitType.Online };
}
