namespace Identity.DTO_s;

public record UpdateMedicalCenterRequest(
    string Name,
    string PrimaryColorHex,
    string FontFamily,
    string ButtonRadius,
    string? BannerVideoUrl
);
