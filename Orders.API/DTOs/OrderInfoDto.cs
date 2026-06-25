using Orders.Models;

namespace Orders.DTOs;

public class OrderInfoDto
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Note { get; set; } 
    public int TotalMinor { get; set; } 
    public List<OrderLineInfoDto> Lines { get; set; }
}