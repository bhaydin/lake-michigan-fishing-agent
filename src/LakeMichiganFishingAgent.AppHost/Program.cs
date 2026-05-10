var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.LakeMichiganFishingAgent_Api>("api");

builder.AddNpmApp("web", "../web", "dev")
    .WithReference(api)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
