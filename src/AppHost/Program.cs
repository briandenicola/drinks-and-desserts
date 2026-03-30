var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WhiskeyAndSmokes_Api>("api")
    .WithExternalHttpEndpoints();

builder.Build().Run();
