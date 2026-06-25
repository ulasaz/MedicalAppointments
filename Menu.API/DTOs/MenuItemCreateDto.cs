namespace Menu.DTOs;

public record MenuItemCreateDto(
     string CategoryName,
     string Name ,
     string Description,
     int PriceMinor,
     bool IsAvailable,
     string? PhotoUrl);