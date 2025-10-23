using IPSDataAcquisitionWorker.Domain.Common;

namespace IPSDataAcquisitionWorker.Domain.Entities;

public class IMUData : BaseEntity
{
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public long Timestamp { get; set; }
    public long? TimestampNanos { get; set; }

    // Calibrated Motion Sensors (15 fields) - All nullable since not all devices have all sensors
    public float? AccelX { get; set; }
    public float? AccelY { get; set; }
    public float? AccelZ { get; set; }
    public float? GyroX { get; set; }
    public float? GyroY { get; set; }
    public float? GyroZ { get; set; }
    public float? MagX { get; set; }
    public float? MagY { get; set; }
    public float? MagZ { get; set; }
    public float? GravityX { get; set; }
    public float? GravityY { get; set; }
    public float? GravityZ { get; set; }
    public float? LinearAccelX { get; set; }
    public float? LinearAccelY { get; set; }
    public float? LinearAccelZ { get; set; }

    // Uncalibrated Sensors (18 fields)
    public float? AccelUncalX { get; set; }
    public float? AccelUncalY { get; set; }
    public float? AccelUncalZ { get; set; }
    public float? AccelBiasX { get; set; }
    public float? AccelBiasY { get; set; }
    public float? AccelBiasZ { get; set; }
    public float? GyroUncalX { get; set; }
    public float? GyroUncalY { get; set; }
    public float? GyroUncalZ { get; set; }
    public float? GyroDriftX { get; set; }
    public float? GyroDriftY { get; set; }
    public float? GyroDriftZ { get; set; }
    public float? MagUncalX { get; set; }
    public float? MagUncalY { get; set; }
    public float? MagUncalZ { get; set; }
    public float? MagBiasX { get; set; }
    public float? MagBiasY { get; set; }
    public float? MagBiasZ { get; set; }

    // Rotation Vectors (12 fields)
    public float? RotationVectorX { get; set; }
    public float? RotationVectorY { get; set; }
    public float? RotationVectorZ { get; set; }
    public float? RotationVectorW { get; set; }
    public float? GameRotationX { get; set; }
    public float? GameRotationY { get; set; }
    public float? GameRotationZ { get; set; }
    public float? GameRotationW { get; set; }
    public float? GeomagRotationX { get; set; }
    public float? GeomagRotationY { get; set; }
    public float? GeomagRotationZ { get; set; }
    public float? GeomagRotationW { get; set; }

    // Environmental Sensors (5 fields)
    public float? Pressure { get; set; }
    public float? Temperature { get; set; }
    public float? Light { get; set; }
    public float? Humidity { get; set; }
    public float? Proximity { get; set; }

    // Activity Sensors (2 fields)
    public int? StepCounter { get; set; }
    public bool? StepDetected { get; set; }

    // Computed Orientation (4 fields)
    public float? Roll { get; set; }
    public float? Pitch { get; set; }
    public float? Yaw { get; set; }
    public float? Heading { get; set; }

    // GPS Data (5 fields)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public float? GpsAccuracy { get; set; }
    public float? Speed { get; set; }

    public bool IsSynced { get; set; } = true;
}

