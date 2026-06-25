using Finbuckle.MultiTenant.Abstractions;

namespace Menu.Models;

[MultiTenant]
public class MenuItem
{
    public Guid Id { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } 
    public string Description { get; set; } 
    public int PriceMinor { get; set; }
    public bool IsAvailable { get; set; } 
    public string? PhotoUrl { get; set; } 
    public DateTime UpdatedAt { get; set; }
    public DateOnly MenuDate { get; set; }

    public Category? Category { get; set; }
}