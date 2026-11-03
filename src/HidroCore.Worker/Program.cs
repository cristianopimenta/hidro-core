using HidroCore.Integrations;
using HidroCore.Infrastructure;
using HidroCore.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHidroCoreInfrastructure(builder.Configuration);
builder.Services.AddHidroCoreIntegrations();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
