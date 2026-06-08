using EarnedScreen.Service;

var builder = Host.CreateApplicationBuilder(args);

// Runs as a Windows Service in production; falls back to a console host when launched directly.
builder.Services.AddWindowsService(options => options.ServiceName = "EarnedScreen");

builder.Services.AddSingleton<EnforcementEngine>();
builder.Services.AddSingleton<EventPipeServer>();
builder.Services.AddSingleton<CommandPipeServer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
