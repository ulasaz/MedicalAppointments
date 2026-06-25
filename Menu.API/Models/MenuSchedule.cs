using Finbuckle.MultiTenant.Abstractions;

namespace Menu.Models;

[MultiTenant]
public class MenuSchedule
{
    public Guid Id { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public Guid MenuItemId { get; set; }

    public MenuItem MenuItem { get; set; }
}