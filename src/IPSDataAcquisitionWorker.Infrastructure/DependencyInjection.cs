using IPSDataAcquisitionWorker.Application.Common.Interfaces;
using IPSDataAcquisitionWorker.Application.Services;
using IPSDataAcquisitionWorker.Infrastructure.Data;
using IPSDataAcquisitionWorker.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IPSDataAcquisitionWorker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - Model First Approach (assumes tables already exist)
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Database connection string 'Default' not found.");
        
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Enable command batching for better bulk insert performance
                npgsqlOptions.MaxBatchSize(100);
                // Set command timeout for large bulk inserts
                npgsqlOptions.CommandTimeout(60);
            });
            options.UseSnakeCaseNamingConvention();
            // Don't ensure database is created - assume it exists
            // Don't run migrations - this is model-first approach
            
            // Performance optimizations
            options.EnableSensitiveDataLogging(false); // Disable in production
            options.EnableDetailedErrors(false); // Disable in production
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        
        // Application Services
        services.AddScoped<IMessageProcessor, IMUDataProcessor>();
        
        // Background Services
        services.AddHostedService<RabbitMqConsumerService>();

        return services;
    }
}

