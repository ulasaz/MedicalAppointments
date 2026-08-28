using Appointments.Models;

namespace Appointments.DTOs;

public class MedicalServiceInfoDto
{
    public Guid Id { get; set; }
    public Guid DoctorProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public List<VisitType> AllowedVisitTypes { get; set; } = new();
}
