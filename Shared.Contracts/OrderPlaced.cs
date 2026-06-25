namespace Shared;

public record OrderPlaced(
    Guid OrderId,
    Guid CustomerId,
    string CustomerName,
    int TotalMinor,
    DateTimeOffset PlacedAt,
    IReadOnlyList<OrderPlacedLine> Lines
);

public record OrderPlacedLine(
    Guid MenuItemId,
    string ItemName,
    int UnitPriceMinor,
    int Quantity
);
