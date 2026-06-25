namespace Orders.DTOs;

public record OrderLineCreateDto(
    Guid MenuItemId,
    int Quantity
);