using System.Text.Json;
using IPSDataAcquisitionWorker.Application.Common.DTOs;
using IPSDataAcquisitionWorker.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IPSDataAcquisitionWorker.Infrastructure.Services;

public class RedisCache : IRedisCache, IDisposable
{
    private readonly ILogger<RedisCache> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _keyPrefix = "imu:session:";
    private readonly TimeSpan _defaultExpiration;

    public RedisCache(IConfiguration configuration, ILogger<RedisCache> logger)
    {
        _logger = logger;
        
        var endpoint = configuration["Redis:Endpoint"];
        
        // Check if Redis is configured and endpoint is not a placeholder
        if (string.IsNullOrEmpty(endpoint) || endpoint.StartsWith("__"))
        {
            _logger.LogWarning("Redis not configured or placeholder not replaced. Redis caching will be disabled.");
            // Create a dummy connection that won't be used
            _redis = null!;
            _db = null!;
            return;
        }
        
        var useSsl = bool.Parse(configuration["Redis:UseSsl"] ?? "true");
        var defaultDatabase = int.Parse(configuration["Redis:Database"] ?? "0");
        _keyPrefix = configuration["Redis:KeyPrefix"] ?? "imu:session:";
        
        var expirationHours = int.Parse(configuration["Redis:ExpirationHours"] ?? "24");
        _defaultExpiration = TimeSpan.FromHours(expirationHours);

        try
        {
            var options = ConfigurationOptions.Parse(endpoint);
            options.Ssl = useSsl;
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 10000;
            options.SyncTimeout = 5000;

            _logger.LogInformation("=== REDIS CONNECTION ATTEMPT ===");
            _logger.LogInformation("Endpoint: {Endpoint}", endpoint);
            _logger.LogInformation("SSL/TLS: {UseSsl}", useSsl);
            _logger.LogInformation("Database: {Database}", defaultDatabase);
            _logger.LogInformation("Key Prefix: {KeyPrefix}", _keyPrefix);
            
            _redis = ConnectionMultiplexer.Connect(options);
            _db = _redis.GetDatabase(defaultDatabase);
            
            var redisEndpoint = _redis.GetEndPoints().FirstOrDefault();
            var isConnected = _redis.IsConnected;
            
            _logger.LogInformation("✅ REDIS CONNECTION SUCCESSFUL");
            _logger.LogInformation("Connected to: {Endpoint}", redisEndpoint);
            _logger.LogInformation("Connection status: {Status}", isConnected ? "Connected" : "Disconnected");
            _logger.LogInformation("Client name: {ClientName}", _redis.ClientName);
            _logger.LogInformation("================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis at {Endpoint}. Redis caching will be disabled.", endpoint);
            _redis = null!;
            _db = null!;
        }
    }

    public async Task AppendSessionDataAsync(string sessionId, List<IMUDataPointDto> dataPoints, CancellationToken cancellationToken = default)
    {
        if (_db == null || string.IsNullOrEmpty(sessionId) || dataPoints == null || !dataPoints.Any())
        {
            return;
        }

        var key = GetKey(sessionId);
        
        try
        {
            // Serialize each data point and add to Redis list
            var values = dataPoints.Select(dp => 
                (RedisValue)JsonSerializer.Serialize(dp)
            ).ToArray();

            // Append to the list (right push)
            await _db.ListRightPushAsync(key, values);
            
            // Set expiration on first write
            await _db.KeyExpireAsync(key, _defaultExpiration);
            
            _logger.LogDebug("Appended {Count} data points to session {SessionId} in Redis", 
                dataPoints.Count, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending data to Redis for session {SessionId}", sessionId);
            // Don't throw - Redis cache failures shouldn't stop processing
        }
    }

    public async Task<List<IMUDataPointDto>?> GetSessionDataAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_db == null || string.IsNullOrEmpty(sessionId))
        {
            return null;
        }

        var key = GetKey(sessionId);
        
        try
        {
            // Get all items from the list
            var values = await _db.ListRangeAsync(key);
            
            if (values.Length == 0)
            {
                return null;
            }

            var dataPoints = new List<IMUDataPointDto>();
            
            foreach (var value in values)
            {
                if (value.HasValue)
                {
                    var dataPoint = JsonSerializer.Deserialize<IMUDataPointDto>(value.ToString());
                    if (dataPoint != null)
                    {
                        dataPoints.Add(dataPoint);
                    }
                }
            }
            
            _logger.LogDebug("Retrieved {Count} data points for session {SessionId} from Redis", 
                dataPoints.Count, sessionId);
            
            return dataPoints;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving data from Redis for session {SessionId}", sessionId);
            return null;
        }
    }

    public async Task DeleteSessionDataAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_db == null || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var key = GetKey(sessionId);
        
        try
        {
            await _db.KeyDeleteAsync(key);
            _logger.LogDebug("Deleted session {SessionId} from Redis", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session {SessionId} from Redis", sessionId);
        }
    }

    public async Task SetSessionExpirationAsync(string sessionId, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        if (_db == null || string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var key = GetKey(sessionId);
        
        try
        {
            await _db.KeyExpireAsync(key, expiration);
            _logger.LogDebug("Set expiration of {Expiration} for session {SessionId}", expiration, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting expiration for session {SessionId}", sessionId);
        }
    }

    public async Task<long> GetSessionDataCountAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (_db == null || string.IsNullOrEmpty(sessionId))
        {
            return 0;
        }

        var key = GetKey(sessionId);
        
        try
        {
            return await _db.ListLengthAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting count for session {SessionId}", sessionId);
            return 0;
        }
    }

    private string GetKey(string sessionId)
    {
        return $"{_keyPrefix}{sessionId}";
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}

