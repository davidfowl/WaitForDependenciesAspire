var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddSqlServer("sql")
    .WithHealthCheck()
    .AddDatabase("db");

var rabbit = builder.AddRabbitMQ("rabbit")
                    .WithHealthCheck();

var console = builder.AddProject<Projects.ConsoleApp1>("console");

builder.AddProject<Projects.WebApplication1>("api")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WithReference(rabbit)
    .WaitOn(db)
    .WaitOn(rabbit)
    .WaitForCompletion(console);

builder.Build().Run();
