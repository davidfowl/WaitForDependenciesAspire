var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql")
    .WithHealthCheck()
    .AddDatabase("db");

var rabbit = builder.AddRabbitMQ("rabbit")
                    .WithHealthCheck();

var console = builder.AddProject<Projects.ConsoleApp1>("console");

var api0 = builder.AddProject<Projects.WebApplication2>("api0")
    .WithHealthCheck();

builder.AddProject<Projects.WebApplication1>("api")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WithReference(rabbit)
    .WaitFor(db)
    .WaitFor(rabbit)
    .WaitFor(api0)
    .WaitForCompletion(console);

builder.Build().Run();
