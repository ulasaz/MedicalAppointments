using MassTransit;
using Shared;
using Microsoft.EntityFrameworkCore;
using Orders.Database;
using Orders.DTOs;
using Orders.Helpers;
using Orders.Interfaces;
using Orders.Models;


namespace Orders.Services;

public class OrderService : IOrderService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly DatabaseContext _dbContext;
    private readonly MenuClient _menuClient;

    public OrderService(DatabaseContext dbContext, MenuClient menuClient, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _menuClient = menuClient;
        this._publishEndpoint = publishEndpoint;
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync(OrderStatus? status = null)
    {
        var queryable = _dbContext.Orders
            .Include(o => o.Lines)
            .AsNoTracking();
        
        if (status.HasValue)
        {
            queryable = queryable.Where(o => o.Status == status.Value);
        }

        return await queryable.ToListAsync();
    }

    public async Task<Order> AddOrderAsync(OrderCreateDto dto, Guid userId, string userName)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = userId,
            CustomerName = userName,
            Note = dto.Note,
            PlacedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending,
            Lines = new List<OrderLine>()
        };
        
        var totalMinor = 0;
        
        foreach (var item in dto.Lines)
        {
            var menuItem = await _menuClient.GetMenuItem(item.MenuItemId);

            if (menuItem == null || !menuItem.IsAvailable)
            {
                throw new ArgumentException("Menu item is not available");
            }
            var lineTotal = menuItem.PriceMinor * item.Quantity;

            var orderLine = new OrderLine
            {
                Id = Guid.NewGuid(),
                ItemNameSnapshot = menuItem.Name,
                LineTotalMinor = lineTotal,
                MenuItemId = menuItem.Id,
                OrderId = order.Id,
                Quantity = item.Quantity,
                UnitPriceMinorSnapshot = menuItem.PriceMinor,
            };
            order.Lines.Add(orderLine);
            totalMinor += lineTotal;
        }
        
        order.TotalMinor = totalMinor;
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        
        var orderPlacedLines = order.Lines.Select(line => new OrderPlacedLine(
            line.MenuItemId,
            line.ItemNameSnapshot,
            line.UnitPriceMinorSnapshot,
            line.Quantity
        )).ToList();
        
        await _publishEndpoint.Publish(new OrderPlaced(
            order.Id, 
            order.CustomerId, 
            order.CustomerName, 
            order.TotalMinor, 
            order.PlacedAt, 
            orderPlacedLines
        ));
        
        return order;
    }

    public async Task<IEnumerable<OrderInfoDto>> GetMyOrdersAsync(Guid userId)
    {

        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == userId)
            .OrderByDescending(o => o.PlacedAt)
            .Select(order => new OrderInfoDto
            {
                Id = order.Id,
                CompletedAt = order.CompletedAt,
                Note = order.Note,
                PlacedAt = order.PlacedAt,
                Status = order.Status,
                TotalMinor = order.TotalMinor,
                Lines = order.Lines.Select(line => new OrderLineInfoDto
                {
                    ItemNameSnapshot = line.ItemNameSnapshot,
                    UnitPriceMinorSnapshot = line.UnitPriceMinorSnapshot,
                    Quantity = line.Quantity,
                    LineTotalMinor = line.LineTotalMinor
                }).ToList()
            }).ToListAsync();
    }

    public async Task<Order> GetOrderByIdAsync(Guid orderId)
    {
        var response = await _dbContext.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        return response ?? throw new ArgumentException("Order not found");
    }

    public async Task<bool> ConfirmOrder(Guid orderId)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new ArgumentException("Order not found");
        }

        if (order.Status == OrderStatus.Pending)
        {
            var oldStatus = order.Status.ToString();
            order.Status = OrderStatus.Confirmed;
            order.ConfirmedAt = DateTime.UtcNow;
            
            await _dbContext.SaveChangesAsync();
            
            await _publishEndpoint.Publish(new OrderStatusChanged(
                order.Id,
                order.CustomerId,
                oldStatus,
                order.Status.ToString(),
                Reason: null,
                DateTimeOffset.UtcNow
            ));
        }
        else
        {
            throw new InvalidOperationException("Can only confirm Pending orders");
        }
        
        return true;
    }

    public async Task<bool> ReadyOrderAsync(Guid orderId)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new ArgumentException("Order not found");
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            var oldStatus = order.Status.ToString();
            order.Status = OrderStatus.Ready;
            
            await _dbContext.SaveChangesAsync();
            
            await _publishEndpoint.Publish(new OrderStatusChanged(
                order.Id,
                order.CustomerId,
                oldStatus,
                order.Status.ToString(),
                Reason: null, 
                DateTimeOffset.UtcNow
            ));
        }
        else
        {
            throw new InvalidOperationException("Can only confirmed make orders ready");
        }
        
        return true;
    }

    public async Task<bool> CompleteOrderAsync(Guid orderId)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new ArgumentException("Order not found");
        }

        if (order.Status == OrderStatus.Ready)
        {
            var oldStatus = order.Status.ToString();
            order.Status = OrderStatus.Completed;
            order.CompletedAt = DateTime.UtcNow;
                    
            await _dbContext.SaveChangesAsync();
            
            await _publishEndpoint.Publish(new OrderStatusChanged(
                order.Id,
                order.CustomerId,
                oldStatus,
                order.Status.ToString(),
                Reason: null, 
                DateTimeOffset.UtcNow
            ));
        }
        else
        {
            throw new InvalidOperationException("Can only ready orders be completed");
        }
        return true;
    }

    public async Task<bool> RejectOrderAsync(Guid orderId, string reason)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new ArgumentException("Order not found");
        }

        if (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Confirmed)
        {
            var oldStatus = order.Status.ToString();
            order.Status = OrderStatus.Rejected;
            order.Note = reason;
            
            await _dbContext.SaveChangesAsync();
            
            await _publishEndpoint.Publish(new OrderStatusChanged(
                order.Id,
                order.CustomerId,
                oldStatus,
                order.Status.ToString(),
                Reason: reason, 
                DateTimeOffset.UtcNow
            ));
        }
        else
        {
            throw new InvalidOperationException("Can only pending orders be rejected");
        }
        
        return true;
    }

    public async Task<bool> CancelOrderAsync(Guid orderId)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            throw new ArgumentException("Order not found");
        }

        if (order.Status == OrderStatus.Pending)
        {
            var oldStatus = order.Status.ToString();
            order.Status = OrderStatus.Cancelled;
            
            await _dbContext.SaveChangesAsync();
            
            await _publishEndpoint.Publish(new OrderStatusChanged(
                order.Id,
                order.CustomerId,
                oldStatus,
                order.Status.ToString(),
                Reason: null, 
                DateTimeOffset.UtcNow
            ));
        }
        else
        {
            throw new InvalidOperationException("Can only pending orders be canceled");
        }
        return true;
    }
}