namespace Menu.DTOs;

public record MenuItemUpdateDto(
    string? CategoryName, 
    string Name,
    string Description, 
    int PriceMinor, 
    bool IsAvailable, 
    string? PhotoUrl
    );