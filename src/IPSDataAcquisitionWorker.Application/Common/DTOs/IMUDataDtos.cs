namespace IPSDataAcquisitionWorker.Application.Common.DTOs;

public record IMUDataQueueMessage
{
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public List<IMUDataPointDto> DataPoints { get; set; } = new();
    public DateTime ReceivedAt { get; set; }
}

public record IMUDataPointDto(
    long Timestamp,
    long? TimestampNanos,
    // Calibrated - All nullable since not all devices have all sensors
    float? AccelX, float? AccelY, float? AccelZ,
    float? GyroX, float? GyroY, float? GyroZ,
    float? MagX, float? MagY, float? MagZ,
    float? GravityX, float? GravityY, float? GravityZ,
    float? LinearAccelX, float? LinearAccelY, float? LinearAccelZ,
    // Uncalibrated
    float? AccelUncalX, float? AccelUncalY, float? AccelUncalZ,
    float? AccelBiasX, float? AccelBiasY, float? AccelBiasZ,
    float? GyroUncalX, float? GyroUncalY, float? GyroUncalZ,
    float? GyroDriftX, float? GyroDriftY, float? GyroDriftZ,
    float? MagUncalX, float? MagUncalY, float? MagUncalZ,
    float? MagBiasX, float? MagBiasY, float? MagBiasZ,
    // Rotation vectors
    float? RotationVectorX, float? RotationVectorY, float? RotationVectorZ, float? RotationVectorW,
    float? GameRotationX, float? GameRotationY, float? GameRotationZ, float? GameRotationW,
    float? GeomagRotationX, float? GeomagRotationY, float? GeomagRotationZ, float? GeomagRotationW,
    // Environmental
    float? Pressure, float? Temperature, float? Light, float? Humidity, float? Proximity,
    // Activity
    int? StepCounter, bool? StepDetected,
    // Computed
    float? Roll, float? Pitch, float? Yaw, float? Heading,
    // GPS
    double? Latitude, double? Longitude, double? Altitude, float? GpsAccuracy, float? Speed
);

