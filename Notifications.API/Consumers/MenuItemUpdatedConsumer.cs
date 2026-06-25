using MassTransit;
using Shared;

namespace Notifications.Consumers;

public class MenuItemUpdatedConsumer : IConsumer<MenuItemUpdated>
{
    public async Task Consume(ConsumeContext<MenuItemUpdated> context)
    {
        var menuItem = context.Message;
        
        Console.WriteLine($"Menu item was changed: {menuItem.MenuItemId}, Name {menuItem.Name}");

        await Task.CompletedTask;
    }
};