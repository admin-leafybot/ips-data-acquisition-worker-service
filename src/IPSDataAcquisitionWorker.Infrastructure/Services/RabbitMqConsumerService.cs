using System.Text;
using System.Text.Json;
using IPSDataAcquisitionWorker.Application.Common.DTOs;
using IPSDataAcquisitionWorker.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IPSDataAcquisitionWorker.Infrastructure.Services;

public class RabbitMqConsumerService : BackgroundService
{
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private IConnection? _connection;
    private IChannel? _channel;
    private SemaphoreSlim? _concurrencySemaphore;

    public RabbitMqConsumerService(
        ILogger<RabbitMqConsumerService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RabbitMQ Consumer Service starting...");

        try
        {
            // Connect to RabbitMQ
            var hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
            var port = int.TryParse(_configuration["RabbitMQ:Port"], out var p) ? p : 5672;
            var userName = _configuration["RabbitMQ:UserName"] ?? "guest";
            var password = _configuration["RabbitMQ:Password"] ?? "guest";
            var queueName = _configuration["RabbitMQ:QueueName"] ?? "imu-data-queue";
            var prefetchCount = ushort.TryParse(_configuration["RabbitMQ:PrefetchCount"], out var pc) ? pc : (ushort)10;
            var maxConcurrency = int.TryParse(_configuration["RabbitMQ:MaxConcurrency"], out var mc) ? mc : 5;

            // Initialize semaphore for concurrent message processing
            _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

            _logger.LogInformation("Connecting to RabbitMQ at {HostName}:{Port}, Queue: {QueueName}, MaxConcurrency: {MaxConcurrency}", 
                hostName, port, queueName, maxConcurrency);

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                Port = port,
                UserName = userName,
                Password = password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            // Enable SSL for port 5671 (Amazon MQ with SSL)
            if (port == 5671)
            {
                factory.Ssl.Enabled = true;
                factory.Ssl.ServerName = hostName;
                factory.Ssl.AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch |
                                                     System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
                _logger.LogInformation("SSL/TLS enabled for RabbitMQ connection");
            }

            _connection = await factory.CreateConnectionAsync(stoppingToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Declare queue (idempotent)
            await _channel.QueueDeclareAsync(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);

            // Set prefetch count (QoS)
            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: prefetchCount, global: false, stoppingToken);

            _logger.LogInformation("Connected to RabbitMQ. Waiting for messages...");

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                // Wait for available slot in the semaphore (concurrency control)
                await _concurrencySemaphore!.WaitAsync(stoppingToken);
                
                // Process message in background task to allow concurrent processing
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        
                        _logger.LogInformation("Received message from queue, Size: {Size} bytes", body.Length);

                        // Deserialize message
                        var queueMessage = JsonSerializer.Deserialize<IMUDataQueueMessage>(message, new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });
                        
                        if (queueMessage == null || queueMessage.DataPoints == null || !queueMessage.DataPoints.Any())
                        {
                            _logger.LogWarning("Invalid message format, rejecting");
                            await _channel.BasicRejectAsync(ea.DeliveryTag, false, stoppingToken);
                            return;
                        }

                        // Process message using scoped service
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
                            await processor.ProcessIMUDataAsync(queueMessage, stoppingToken);
                        }

                        // Acknowledge message
                        await _channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
                        
                        _logger.LogInformation("Successfully processed {Count} IMU data points for session {SessionId}",
                            queueMessage.DataPoints.Count, queueMessage.SessionId ?? "null");
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error deserializing message, rejecting without requeue");
                        // Don't requeue invalid messages
                        await _channel.BasicRejectAsync(ea.DeliveryTag, false, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message, rejecting with requeue for retry");
                        // Requeue for retry on processing errors
                        await _channel.BasicRejectAsync(ea.DeliveryTag, true, stoppingToken);
                    }
                    finally
                    {
                        // Release semaphore slot
                        _concurrencySemaphore.Release();
                    }
                }, stoppingToken);
            };

            await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("RabbitMQ Consumer Service stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in RabbitMQ Consumer Service");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping RabbitMQ Consumer Service...");
        
        if (_channel != null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        
        if (_connection != null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }

        _concurrencySemaphore?.Dispose();

        await base.StopAsync(stoppingToken);
    }
}

