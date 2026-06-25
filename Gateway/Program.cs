var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularApplicationPolicy", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://cafe1.localhost:4200",
                "http://cafe2.localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.MapHealthChecks("/health");
app.UseCors("AngularApplicationPolicy");
app.MapReverseProxy();

app.Run();