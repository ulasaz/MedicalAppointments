using Menu.DTOs;
using Menu.Models;

namespace Menu.Interfaces;

public interface IMenuService
{ 
    Task<IEnumerable<MenuItem>> GetAllMenuItemsAsync(string? categoryName, bool? availability);
    Task<IEnumerable<MenuItem>> GetAllMenuItemsUnscheduledAsync();
    Task<bool> DeleteMenuItemAsync(Guid id);
    Task<MenuItemCreateDto> AddMenuItemAsync(MenuItemCreateDto menuItem);
    Task<MenuItemUpdateDto> UpdateMenuItemAsync(Guid id, MenuItemUpdateDto menuItem);
    Task<MenuItem> GetMenuItemByIdAsync(Guid id);
    Task<bool> ToggleAvailabilityAsync(Guid id);
    Task<IEnumerable<MenuSchedule>> GetScheduleForDayAsync(DayOfWeek day);
    Task<MenuSchedule> AddToScheduleAsync(MenuScheduleCreateDto dto);
    Task<bool> RemoveFromScheduleAsync(Guid scheduleId);
}