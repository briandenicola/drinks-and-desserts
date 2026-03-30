var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WhiskeyAndSmokes_Api>("api")
    .WithHttpEndpoint(port: 5062, name: "http")
    .WithExternalHttpEndpoints();

builder.Build().Run();
