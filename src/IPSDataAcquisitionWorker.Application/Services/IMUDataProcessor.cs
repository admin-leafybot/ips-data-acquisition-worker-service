using IPSDataAcquisitionWorker.Application.Common.DTOs;
using IPSDataAcquisitionWorker.Application.Common.Interfaces;
using IPSDataAcquisitionWorker.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IPSDataAcquisitionWorker.Application.Services;

public class IMUDataProcessor : IMessageProcessor
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<IMUDataProcessor> _logger;

    public IMUDataProcessor(
        IApplicationDbContext context,
        ILogger<IMUDataProcessor> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ProcessIMUDataAsync(IMUDataQueueMessage message, CancellationToken cancellationToken = default)
    {
        if (message?.DataPoints == null || !message.DataPoints.Any())
        {
            _logger.LogWarning("Received empty or null IMU data message");
            return;
        }

        _logger.LogInformation("Processing {Count} IMU data points for session {SessionId}, user {UserId}",
            message.DataPoints.Count, message.SessionId ?? "null", message.UserId ?? "null");

        var startTime = DateTime.UtcNow;
        var imuDataList = new List<IMUData>(message.DataPoints.Count); // Pre-allocate capacity

        foreach (var point in message.DataPoints)
        {
            var imuData = new IMUData
            {
                SessionId = message.SessionId,
                UserId = message.UserId,
                Timestamp = point.Timestamp,
                TimestampNanos = point.TimestampNanos,
                
                // Calibrated Motion Sensors
                AccelX = point.AccelX, AccelY = point.AccelY, AccelZ = point.AccelZ,
                GyroX = point.GyroX, GyroY = point.GyroY, GyroZ = point.GyroZ,
                MagX = point.MagX, MagY = point.MagY, MagZ = point.MagZ,
                GravityX = point.GravityX, GravityY = point.GravityY, GravityZ = point.GravityZ,
                LinearAccelX = point.LinearAccelX, LinearAccelY = point.LinearAccelY, LinearAccelZ = point.LinearAccelZ,
                
                // Uncalibrated Sensors
                AccelUncalX = point.AccelUncalX, AccelUncalY = point.AccelUncalY, AccelUncalZ = point.AccelUncalZ,
                AccelBiasX = point.AccelBiasX, AccelBiasY = point.AccelBiasY, AccelBiasZ = point.AccelBiasZ,
                GyroUncalX = point.GyroUncalX, GyroUncalY = point.GyroUncalY, GyroUncalZ = point.GyroUncalZ,
                GyroDriftX = point.GyroDriftX, GyroDriftY = point.GyroDriftY, GyroDriftZ = point.GyroDriftZ,
                MagUncalX = point.MagUncalX, MagUncalY = point.MagUncalY, MagUncalZ = point.MagUncalZ,
                MagBiasX = point.MagBiasX, MagBiasY = point.MagBiasY, MagBiasZ = point.MagBiasZ,
                
                // Rotation Vectors
                RotationVectorX = point.RotationVectorX, RotationVectorY = point.RotationVectorY,
                RotationVectorZ = point.RotationVectorZ, RotationVectorW = point.RotationVectorW,
                GameRotationX = point.GameRotationX, GameRotationY = point.GameRotationY,
                GameRotationZ = point.GameRotationZ, GameRotationW = point.GameRotationW,
                GeomagRotationX = point.GeomagRotationX, GeomagRotationY = point.GeomagRotationY,
                GeomagRotationZ = point.GeomagRotationZ, GeomagRotationW = point.GeomagRotationW,
                
                // Environmental Sensors
                Pressure = point.Pressure, Temperature = point.Temperature, Light = point.Light,
                Humidity = point.Humidity, Proximity = point.Proximity,
                
                // Activity Sensors
                StepCounter = point.StepCounter, StepDetected = point.StepDetected,
                
                // Computed Orientation
                Roll = point.Roll, Pitch = point.Pitch, Yaw = point.Yaw, Heading = point.Heading,
                
                // GPS Data
                Latitude = point.Latitude, Longitude = point.Longitude, Altitude = point.Altitude,
                GpsAccuracy = point.GpsAccuracy, Speed = point.Speed,
                
                IsSynced = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            imuDataList.Add(imuData);
        }

        // Bulk insert for better performance
        await _context.IMUData.AddRangeAsync(imuDataList, cancellationToken);
        var recordsSaved = await _context.SaveChangesAsync(cancellationToken);

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var throughput = recordsSaved / (duration / 1000.0);
        
        _logger.LogInformation("Successfully saved {Count} IMU data points to database in {Duration}ms ({Throughput:F0} records/sec)", 
            recordsSaved, duration, throughput);
    }
}

