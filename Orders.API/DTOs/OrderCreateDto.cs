using Orders.Models;

namespace Orders.DTOs;


public record OrderCreateDto(
    string? Note, 
    List<OrderLineCreateDto> Lines
);