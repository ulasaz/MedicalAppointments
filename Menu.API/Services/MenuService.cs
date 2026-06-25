using MassTransit;
using Menu.Database;
using Menu.DTOs;
using Menu.Interfaces;
using Menu.Models;
using Microsoft.EntityFrameworkCore;
using Shared;

namespace Menu.Services;

public class MenuService : IMenuService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly DatabaseContext _dbContext;

    public MenuService(DatabaseContext dbContext, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<IEnumerable<MenuItem>> GetAllMenuItemsAsync(string? categoryName, bool? availability)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _dbContext.MenuItems
            .Include(m => m.Category)
            .Where(m => m.MenuDate == today)
            .AsQueryable();

        if (!string.IsNullOrEmpty(categoryName))
            query = query.Where(m => m.Category != null && m.Category.Name == categoryName);

        if (availability != null)
            query = query.Where(m => m.IsAvailable == availability);

        return await query.ToListAsync();
    }

    public async Task<IEnumerable<MenuItem>> GetAllMenuItemsUnscheduledAsync()
    {
        return await _dbContext.MenuItems.AsNoTracking().ToListAsync();
    }

    public async Task<bool> DeleteMenuItemAsync(Guid id)
    {
        var itemToDelete = await _dbContext.MenuItems.FindAsync(id);
        
        if (itemToDelete == null)
        {
            throw new KeyNotFoundException("Item does not exist"); 
        }
        
        _dbContext.MenuItems.Remove(itemToDelete);
        await _dbContext.SaveChangesAsync();
        
        return true;
    }
    

    public async Task<MenuItem> GetMenuItemByIdAsync(Guid id)
    {
        var existingItem = await _dbContext.MenuItems.FindAsync(id);
        
        if (existingItem == null)
        {
            throw new KeyNotFoundException("Item does not exist"); 
        }
        
        return existingItem;
    }
    

    public async Task<MenuItemCreateDto> AddMenuItemAsync(MenuItemCreateDto menuItem)
    {
        ArgumentNullException.ThrowIfNull(menuItem);

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == menuItem.CategoryName);
        if (category == null)
        {
            if (string.IsNullOrWhiteSpace(menuItem.CategoryName))
                throw new ArgumentException("Category does not exist");

            category = new Category { Id = Guid.NewGuid(), Name = menuItem.CategoryName, SortOrder = 0 };
            await _dbContext.Categories.AddAsync(category);
            await _dbContext.SaveChangesAsync();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existingMenuItem = await _dbContext.MenuItems.FirstOrDefaultAsync(i => i.Name == menuItem.Name);
        if (existingMenuItem != null)
        {
            existingMenuItem.MenuDate = today;
            existingMenuItem.CategoryId = category.Id;
            existingMenuItem.PriceMinor = menuItem.PriceMinor;
            existingMenuItem.IsAvailable = menuItem.IsAvailable;
            existingMenuItem.PhotoUrl = menuItem.PhotoUrl;
            existingMenuItem.Description = menuItem.Description;
            existingMenuItem.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await _dbContext.AddAsync(new MenuItem
            {
                Name = menuItem.Name,
                CategoryId = category.Id,
                Description = menuItem.Description,
                PriceMinor = menuItem.PriceMinor,
                IsAvailable = menuItem.IsAvailable,
                PhotoUrl = menuItem.PhotoUrl,
                UpdatedAt = DateTime.UtcNow,
                MenuDate = today
            });
        }

        await _dbContext.SaveChangesAsync();
        return menuItem;
    }
    

    public async Task<MenuItemUpdateDto> UpdateMenuItemAsync(Guid id, MenuItemUpdateDto menuItem)
    {
        ArgumentNullException.ThrowIfNull(menuItem);
        
        var existingMenuItem = await _dbContext.MenuItems.FirstOrDefaultAsync(i => i.Id == id);
            
        if (existingMenuItem == null)
        {
            throw new KeyNotFoundException("Item does not exist"); 
        }
        
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Name == menuItem.CategoryName);
            
        if (category == null)
        {
            throw new ArgumentException($"Category does not exist");
        }
            
        existingMenuItem.CategoryId = category.Id;
        existingMenuItem.Name = menuItem.Name;
        existingMenuItem.Description = menuItem.Description;
        existingMenuItem.PriceMinor = menuItem.PriceMinor;
        existingMenuItem.IsAvailable = menuItem.IsAvailable;
        existingMenuItem.PhotoUrl = menuItem.PhotoUrl;
        existingMenuItem.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        
        await _publishEndpoint.Publish(new MenuItemUpdated(
            existingMenuItem.Id, 
            existingMenuItem.Name, 
            existingMenuItem.PriceMinor, 
            existingMenuItem.IsAvailable,
            existingMenuItem.UpdatedAt));
        
        return menuItem;
    }
    
    public async Task<bool> ToggleAvailabilityAsync(Guid id)
    {
        var existingMenuItem = await _dbContext.MenuItems.FindAsync(id);

        if (existingMenuItem == null)
        {
            throw new KeyNotFoundException("Item does not exist");
        }

        existingMenuItem.IsAvailable = !existingMenuItem.IsAvailable;
        existingMenuItem.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _publishEndpoint.Publish(new MenuItemUpdated(
            existingMenuItem.Id,
            existingMenuItem.Name,
            existingMenuItem.PriceMinor,
            existingMenuItem.IsAvailable,
            existingMenuItem.UpdatedAt));

        return true;
    }

    public async Task<IEnumerable<MenuSchedule>> GetScheduleForDayAsync(DayOfWeek day)
    {
        return await _dbContext.MenuSchedules
            .Where(s => s.DayOfWeek == day)
            .Include(s => s.MenuItem)
            .ToListAsync();
    }

    public async Task<MenuSchedule> AddToScheduleAsync(MenuScheduleCreateDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.menuItemId == null || dto.dayOfWeek == null)
        {
            throw new ArgumentException("MenuItemId and DayOfWeek are required");
        }

        var menuItem = await _dbContext.MenuItems.FindAsync(dto.menuItemId.Value);

        if (menuItem == null)
        {
            throw new KeyNotFoundException("Menu item does not exist");
        }

        var duplicate = await _dbContext.MenuSchedules.FirstOrDefaultAsync(
            s => s.MenuItemId == dto.menuItemId.Value && s.DayOfWeek == dto.dayOfWeek.Value);

        if (duplicate != null)
        {
            throw new ArgumentException("This item is already scheduled for that day");
        }

        var schedule = new MenuSchedule
        {
            Id = Guid.NewGuid(),
            MenuItemId = dto.menuItemId.Value,
            DayOfWeek = dto.dayOfWeek.Value
        };

        await _dbContext.AddAsync(schedule);
        await _dbContext.SaveChangesAsync();

        return schedule;
    }

    public async Task<bool> RemoveFromScheduleAsync(Guid scheduleId)
    {
        var schedule = await _dbContext.MenuSchedules.FindAsync(scheduleId);

        if (schedule == null)
        {
            throw new KeyNotFoundException("Schedule entry does not exist");
        }

        _dbContext.MenuSchedules.Remove(schedule);
        await _dbContext.SaveChangesAsync();

        return true;
    }
}