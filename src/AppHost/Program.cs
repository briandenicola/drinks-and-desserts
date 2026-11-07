var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WhiskeyAndSmokes_Api>("api")
    .WithExternalHttpEndpoints()
    .WithEnvironment("AiFoundry__Endpoint", builder.Configuration["AiFoundry:Endpoint"] ?? "")
    .WithEnvironment("AiFoundry__ProjectEndpoint", builder.Configuration["AiFoundry:ProjectEndpoint"] ?? "")
    // Optional seed only. The effective models are chosen in-app (Admin → Foundry).
    .WithEnvironment("AiFoundry__Models__Vision", builder.Configuration["AiFoundry:Models:Vision"] ?? "")
    .WithEnvironment("AiFoundry__Models__Reasoning", builder.Configuration["AiFoundry:Models:Reasoning"] ?? "")
    .WithEnvironment("APPLICATIONINSIGHTS_CONNECTION_STRING",
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? "");

builder.Build().Run();
