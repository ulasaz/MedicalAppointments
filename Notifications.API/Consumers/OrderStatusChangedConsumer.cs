using MassTransit;
using Shared;

namespace Notifications.Consumers;

public class OrderStatusChangedConsumer : IConsumer<OrderStatusChanged>
{
    public async Task Consume(ConsumeContext<OrderStatusChanged> context)
    {
        var order = context.Message;
        
        Console.WriteLine($" Order {order.OrderId} changed status from: {order.OldStatus} to: {order.NewStatus}!");

        await Task.CompletedTask;
    }
}