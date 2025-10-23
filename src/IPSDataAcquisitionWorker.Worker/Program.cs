using IPSDataAcquisitionWorker.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Add Infrastructure services (DbContext, RabbitMQ, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();

// Log startup
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("IPSDataAcquisition Worker Service starting up...");
logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);

host.Run();
