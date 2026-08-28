namespace Identity.DTO_s;

public record MedicalCenterDto(
    Guid Id,
    string Name,
    string Slug,
    string PrimaryColorHex,
    bool IsActive,
    DateTime CreatedAt,
    string FontFamily,
    string ButtonRadius,
    string? BannerVideoUrl,
    bool HasBannerImage
);
