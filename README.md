# Waiting for dependencies

This examples shows how to extend the .NET Aspire application model to enable waiting for dependencies to be available before starting the application. It uses ASP.NET Core's [health checks API](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-8.0) to determine if specific resources are available after they are considered running.


```C#
var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql")
    .WithHealthCheck()
    .AddDatabase("db");

var rabbit = builder.AddRabbitMQ("rabbit")
                    .WithHealthCheck();

builder.AddProject<Projects.WebApplication1>("api")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WithReference(rabbit)
    .WaitOn(db)
    .WaitOn(rabbit);

builder.Build().Run();
```

The above example shows the usage. `WaitOn` is an extension method that stores a 
reference to the dependency and waits for it to be healthy before starting a specific resource. In the above case,
the `api` project will wait for the `db` and `rabbit` dependencies to be healthy before starting.