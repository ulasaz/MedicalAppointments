using Finbuckle.MultiTenant.Abstractions;

namespace Orders.Models;

[MultiTenant]
public class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; } 
    public int TotalMinor { get; set; } 
    public List<OrderLine> Lines { get; set; }
}
