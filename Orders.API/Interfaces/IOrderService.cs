using Orders.DTOs;
using Orders.Models;

namespace Orders.Interfaces;

public interface IOrderService
{
    Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null);
    Task<Order> AddOrderAsync(OrderCreateDto dto, Guid userId, string userName);
    Task<IEnumerable<OrderInfoDto>> GetMyOrdersAsync(Guid userId);
    Task<Order> GetOrderByIdAsync(Guid orderId);
    Task<bool> ConfirmOrder(Guid orderId);
    Task<bool> RejectOrderAsync(Guid orderId, string reason);
    Task<bool> ReadyOrderAsync(Guid orderId);
    Task<bool> CompleteOrderAsync(Guid orderId);
    Task<bool> CancelOrderAsync(Guid orderId);
}