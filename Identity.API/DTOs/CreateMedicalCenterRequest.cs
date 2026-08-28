namespace Identity.DTO_s;

/// <summary>Bootstraps a new medical center together with its first (center-scoped) admin
/// account in one step — a center can't exist without someone able to manage it.</summary>
public record CreateMedicalCenterRequest(
    string Name,
    string Slug,
    string PrimaryColorHex,
    string AdminEmail,
    string AdminPassword,
    string AdminDisplayName
);
