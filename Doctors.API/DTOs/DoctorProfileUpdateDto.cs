namespace Doctors.DTOs;

public record DoctorProfileUpdateDto(
    string FullName,
    string Specialization,
    string City,
    string? Description,
    bool IsActive,
    int? PriceStationaryCents,
    int? PriceOnlineCents,
    List<string>? ConditionsTreated);
