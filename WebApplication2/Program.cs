using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

int times = 0;

builder.Services.AddHealthChecks().AddAsyncCheck("c0", async () =>
{
    times++;
    await Task.Delay(1000);

    if (times > 2)
    {
        return HealthCheckResult.Healthy();
    }

    return HealthCheckResult.Unhealthy();
});

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapDefaultEndpoints();

app.Run();
