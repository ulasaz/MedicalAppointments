namespace Shared;

public record MenuItemUpdated(
    Guid MenuItemId,
    string Name,
    int PriceMinor,
    bool IsAvailable,
    DateTimeOffset UpdatedAt
);
