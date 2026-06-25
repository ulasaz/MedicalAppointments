namespace Menu.DTOs;

public record MenuScheduleCreateDto(
    DayOfWeek? dayOfWeek,
    Guid? menuItemId
);