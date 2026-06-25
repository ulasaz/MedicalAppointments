namespace Shared;

public record MenuItemDto(
    Guid Id,
    string Name,
    int PriceMinor,
    bool IsAvailable);