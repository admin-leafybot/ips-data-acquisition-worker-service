using IPSDataAcquisitionWorker.Application.Common.DTOs;

namespace IPSDataAcquisitionWorker.Application.Common.Interfaces;

public interface IRedisCache
{
    /// <summary>
    /// Append IMU data points to a session's cache
    /// </summary>
    Task AppendSessionDataAsync(string sessionId, List<IMUDataPointDto> dataPoints, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all cached data points for a session
    /// </summary>
    Task<List<IMUDataPointDto>?> GetSessionDataAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete session data from cache
    /// </summary>
    Task DeleteSessionDataAsync(string sessionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Set expiration for session data (default: 24 hours)
    /// </summary>
    Task SetSessionExpirationAsync(string sessionId, TimeSpan expiration, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get count of data points for a session
    /// </summary>
    Task<long> GetSessionDataCountAsync(string sessionId, CancellationToken cancellationToken = default);
}

