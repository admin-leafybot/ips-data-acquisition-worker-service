# IPS Data Acquisition Worker Service

A .NET 9.0 background worker service that consumes IMU (Inertial Measurement Unit) data from RabbitMQ and saves it to PostgreSQL database. Built with **Clean Architecture** principles.

## 🏗️ Architecture

This service follows Clean Architecture with clear separation of concerns:

- **Domain Layer**: Core business entities (IMUData)
- **Application Layer**: Business logic, DTOs, and interfaces
- **Infrastructure Layer**: Database access (EF Core), RabbitMQ consumer
- **Worker Layer**: Application entry point and configuration

**Model-First Approach**: This service does **NOT** create or manage database migrations. It assumes that database tables already exist (managed by the API project).

## 📋 Features

- ✅ **Clean Architecture** - Maintainable and testable codebase
- ✅ **Model-First** - No migrations, uses existing database schema
- ✅ **RabbitMQ Consumer** - Asynchronous message processing
- ✅ **Concurrent Processing** - Process multiple messages simultaneously
- ✅ **Bulk Insert** - Efficient batch processing for IMU data (up to 2000+ records/sec per worker)
- ✅ **Docker Support** - Containerized for easy deployment
- ✅ **AWS Ready** - GitHub Actions workflow for ECR & EC2 deployment
- ✅ **Auto-Retry** - Failed messages are automatically requeued
- ✅ **Auto-Reconnect** - RabbitMQ connection auto-recovery
- ✅ **QoS Control** - Configurable prefetch count for load management
- ✅ **Performance Tuning** - Configurable concurrency and batching
- ✅ **Structured Logging** - Clear logging with performance metrics

## 🚀 Quick Start

### Prerequisites

- .NET 9.0 SDK
- Docker & Docker Compose
- PostgreSQL 16 (for local dev)
- RabbitMQ 3 (for local dev)

### Local Development (Docker Compose)

```bash
# Clone the repository
git clone <repository-url>
cd ips-data-acquisition-worker-service

# Build and run with Docker Compose
docker-compose up -d

# View logs
docker-compose logs -f worker
```

### Local Development (Without Docker)

```bash
# Restore dependencies
dotnet restore

# Run the worker
dotnet run --project src/IPSDataAcquisitionWorker.Worker

# Or watch mode for development
dotnet watch --project src/IPSDataAcquisitionWorker.Worker
```

## ⚙️ Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=ips_data_acquisition;Username=postgres;Password=postgres"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "QueueName": "imu-data-queue",
    "PrefetchCount": 20,
    "MaxConcurrency": 10
  }
}
```

### Environment Variables

You can also configure via environment variables (useful for Docker):

```bash
ConnectionStrings__Default="Host=db;Port=5432;Database=ips_data_acquisition;Username=postgres;Password=postgres"
RabbitMQ__HostName=rabbitmq
RabbitMQ__Port=5672
RabbitMQ__UserName=guest
RabbitMQ__Password=guest
RabbitMQ__QueueName=imu-data-queue
RabbitMQ__PrefetchCount=20
RabbitMQ__MaxConcurrency=10
```

## 📦 Project Structure

```
src/
├── IPSDataAcquisitionWorker.Domain/
│   ├── Common/
│   │   └── BaseEntity.cs
│   └── Entities/
│       └── IMUData.cs
├── IPSDataAcquisitionWorker.Application/
│   ├── Common/
│   │   ├── DTOs/
│   │   │   └── IMUDataDtos.cs
│   │   └── Interfaces/
│   │       ├── IApplicationDbContext.cs
│   │       └── IMessageProcessor.cs
│   └── Services/
│       └── IMUDataProcessor.cs
├── IPSDataAcquisitionWorker.Infrastructure/
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   ├── Services/
│   │   └── RabbitMqConsumerService.cs
│   └── DependencyInjection.cs
└── IPSDataAcquisitionWorker.Worker/
    ├── Program.cs
    ├── appsettings.json
    ├── appsettings.Development.json
    └── appsettings.Production.json
```

## 🔄 How It Works

1. **Worker starts** and connects to RabbitMQ with auto-recovery
2. **Listens** to `imu-data-queue` for messages
3. **Receives** multiple messages (up to `PrefetchCount`)
4. **Processes concurrently** (up to `MaxConcurrency` messages in parallel)
5. **Deserializes** JSON message into DTOs
6. **Maps** DTOs to domain entities  
7. **Bulk inserts** into PostgreSQL database (batched for performance)
8. **Acknowledges** message (or requeues on error)
9. **Logs performance** metrics (throughput, duration)

**Performance:** Can handle 100+ messages/sec (100,000+ records/sec) per worker instance.

### Message Format

```json
{
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "userId": "user123",
  "dataPoints": [
    {
      "timestamp": 1698345600000,
      "timestampNanos": 123456789,
      "accelX": 0.5, "accelY": 0.3, "accelZ": 9.8,
      "gyroX": 0.1, "gyroY": 0.2, "gyroZ": 0.3,
      "latitude": 37.7749, "longitude": -122.4194,
      ...
    }
  ],
  "receivedAt": "2024-10-23T10:30:00Z"
}
```

## 🐳 Docker

### Build Docker Image

```bash
docker build -t ips-worker:latest .
```

### Run Container

```bash
docker run -d \
  --name ips-worker \
  -e ConnectionStrings__Default="Host=db;..." \
  -e RabbitMQ__HostName=rabbitmq \
  ips-worker:latest
```

## ⚡ Performance Tuning

For high-throughput scenarios (e.g., 100 devices sending 1000 records every 3 seconds):

**See [PERFORMANCE_TUNING.md](./PERFORMANCE_TUNING.md) for detailed performance optimization guide.**

**Quick tips:**
- **MaxConcurrency**: Number of messages processed in parallel (default: 10)
- **PrefetchCount**: Messages RabbitMQ delivers ahead (default: 20)
- **Recommended for 100 devices**: Single `t3.large` worker handles the load easily
- **Scaling**: Horizontal scaling (multiple workers) recommended over vertical

**Expected Throughput:**
- Single worker (8 cores): ~400-500 messages/sec (~400,000 records/sec)
- With your load (33 msg/sec): 10-15x headroom

## ☁️ AWS Deployment

See [AWS_DEPLOYMENT.md](./AWS_DEPLOYMENT.md) for detailed deployment instructions.

### GitHub Secrets Required

- `AWS_REGION` - AWS region (e.g., ap-south-1)
- `AWS_ACCOUNT_ID` - Your AWS account ID
- `ECR_REPOSITORY_WORKER` - ECR repository name for worker
- `DB_CONNECTION_STRING` - Production database connection string
- `RABBITMQ_HOST` - RabbitMQ host
- `RABBITMQ_USER` - RabbitMQ username
- `RABBITMQ_PASSWORD` - RabbitMQ password
- `EC2_HOST` - EC2 instance public IP/hostname
- `EC2_USER` - EC2 SSH username (e.g., ubuntu)
- `EC2_SSH_KEY` - EC2 SSH private key
- `AWS_ACCESS_KEY_ID` - AWS access key for deployment
- `AWS_SECRET_ACCESS_KEY` - AWS secret key for deployment

### Deployment Workflow

Push to `main` branch triggers:
1. Build .NET application
2. Replace placeholders in appsettings with secrets
3. Build Docker image
4. Push to AWS ECR
5. SSH to EC2 and deploy via docker-compose

## 📊 Monitoring

### View Worker Logs

```bash
# Docker Compose
docker-compose logs -f worker

# Docker
docker logs -f ips-worker

# EC2 (production)
ssh ubuntu@<ec2-ip>
cd ~/ips-data-acquisition-worker
sudo docker-compose -f docker-compose.prod.yml logs -f
```

### RabbitMQ Management UI

Access at: `http://localhost:15672` (guest/guest)

- View queue depth
- Monitor message processing rate
- Check for dead-letter messages

### Database Queries

```sql
-- Check recent IMU data
SELECT * FROM imu_data ORDER BY created_at DESC LIMIT 10;

-- Count by session
SELECT session_id, COUNT(*) FROM imu_data GROUP BY session_id;

-- Check processing performance
SELECT 
  DATE_TRUNC('minute', created_at) as minute,
  COUNT(*) as records
FROM imu_data
WHERE created_at > NOW() - INTERVAL '1 hour'
GROUP BY minute
ORDER BY minute DESC;
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

## 🔧 Troubleshooting

### Worker not connecting to RabbitMQ

- Check RabbitMQ is running: `docker ps | grep rabbitmq`
- Verify connection string in appsettings
- Check RabbitMQ logs: `docker logs ips-rabbitmq`
- Connection will auto-retry every 10 seconds

### Messages not being processed fast enough

- Increase `MaxConcurrency` (e.g., 10 → 20)
- Increase `PrefetchCount` (e.g., 20 → 50)
- Check database performance (slow queries)
- Consider horizontal scaling (add more workers)
- See [PERFORMANCE_TUNING.md](./PERFORMANCE_TUNING.md)

### Messages not being processed

- Check queue exists in RabbitMQ Management UI
- Verify queue name matches in worker config
- Check worker logs for errors
- Monitor RabbitMQ queue depth

### Database connection issues

- Ensure PostgreSQL is running
- Verify connection string
- Ensure database tables exist (run API migrations first)
- Check database connection pool settings

### High CPU or memory usage

- Reduce `MaxConcurrency` if too high
- Reduce `PrefetchCount` if messages are large
- Scale horizontally instead of vertically
- See [PERFORMANCE_TUNING.md](./PERFORMANCE_TUNING.md)

## 📚 Related Projects

- [IPS Data Acquisition API](../ips-data-acquisition-api) - REST API that publishes IMU data to RabbitMQ

## 📄 License

[Add your license here]

## 👥 Contributors

[Add contributors here]

