using MassTransit;
using Notifications.Consumers;
using Notifications.Helpers;
using Notifications.Interfaces;
using Notifications.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddHttpClient<IIdentityClient, IdentityClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:IdentityApi"] ?? "http://localhost:5001"));

builder.Services.AddHttpClient<IDoctorsClient, DoctorsClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:DoctorsApi"] ?? "http://localhost:5002"));

builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AppointmentRequestedConsumer>();
    x.AddConsumer<AppointmentConfirmedConsumer>();
    x.AddConsumer<AppointmentCancelledConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});


var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();