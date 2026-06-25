using Finbuckle.MultiTenant.Abstractions;

namespace Orders.Models;

[MultiTenant]
public class OrderLine
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }
    public string ItemNameSnapshot { get; set; }
    public int UnitPriceMinorSnapshot { get; set; }
    public int Quantity { get; set; }
    public int LineTotalMinor { get; set; }
    
}