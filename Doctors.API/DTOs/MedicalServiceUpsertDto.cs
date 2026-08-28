using Doctors.Models;

namespace Doctors.DTOs;

public record MedicalServiceUpsertDto(
    string Name,
    string? Description,
    int PriceCents,
    List<VisitType> AllowedVisitTypes);
