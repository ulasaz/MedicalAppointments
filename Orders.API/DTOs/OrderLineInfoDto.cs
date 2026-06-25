namespace Orders.DTOs;

public record OrderLineInfoDto {
    public string ItemNameSnapshot { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public int LineTotalMinor { get; init; }
    public int UnitPriceMinorSnapshot { get; set; }
}