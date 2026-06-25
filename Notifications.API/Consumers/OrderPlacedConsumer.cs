using MassTransit;
using Shared;

namespace Notifications.Consumers;

public class OrderPlacedConsumer : IConsumer<OrderPlaced>
{
    public async Task Consume(ConsumeContext<OrderPlaced> context)
    {
        var order = context.Message;
        
        Console.WriteLine($"Accept new Order {order.OrderId} fro client {order.CustomerName}!");

        await Task.CompletedTask;
    }
}