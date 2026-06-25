using MassTransit;
using Notifications.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderPlacedConsumer>();
    x.AddConsumer<OrderStatusChangedConsumer>();
    x.AddConsumer<MenuItemUpdatedConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});


var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();