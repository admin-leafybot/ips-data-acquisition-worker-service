# Architecture Documentation

## Overview

The IPS Data Acquisition Worker Service is built using **Clean Architecture** principles, ensuring maintainability, testability, and separation of concerns. This document describes the architecture, design decisions, and patterns used.

## Clean Architecture Layers

```
┌─────────────────────────────────────────────────────┐
│                  Worker Layer                       │
│            (Entry Point & Composition)              │
│  • Program.cs                                       │
│  • Configuration                                    │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│             Infrastructure Layer                    │
│      (External Services & Persistence)              │
│  • Database (EF Core + PostgreSQL)                  │
│  • RabbitMQ Consumer                                │
│  • Dependency Injection                             │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│             Application Layer                       │
│          (Business Logic & DTOs)                    │
│  • IMUDataProcessor                                 │
│  • Interfaces                                       │
│  • DTOs                                             │
└──────────────────┬──────────────────────────────────┘
                   │
┌──────────────────▼──────────────────────────────────┐
│              Domain Layer                           │
│         (Core Business Entities)                    │
│  • IMUData Entity                                   │
│  • BaseEntity                                       │
└─────────────────────────────────────────────────────┘
```

## Layer Details

### 1. Domain Layer (`IPSDataAcquisitionWorker.Domain`)

**Purpose**: Contains core business entities and domain logic.

**Key Components**:
- `BaseEntity.cs` - Base class for all entities with common properties (Id, CreatedAt, UpdatedAt)
- `IMUData.cs` - Entity representing IMU sensor data with 60+ fields

**Characteristics**:
- ✅ No dependencies on other layers
- ✅ Pure business logic
- ✅ Framework-agnostic
- ✅ Highly testable

**Design Decisions**:
- All sensor fields are nullable - not all devices have all sensors
- Follows the same schema as the API project for consistency
- Uses `Guid` for primary keys
- Includes timestamp tracking (CreatedAt, UpdatedAt)

### 2. Application Layer (`IPSDataAcquisitionWorker.Application`)

**Purpose**: Contains application business logic, DTOs, and interfaces.

**Key Components**:

#### DTOs
- `IMUDataQueueMessage` - Message format received from RabbitMQ
- `IMUDataPointDto` - Individual sensor reading within a message

#### Interfaces
- `IApplicationDbContext` - Database context abstraction
- `IMessageProcessor` - Message processing abstraction

#### Services
- `IMUDataProcessor` - Core business logic for processing IMU data
  - Maps DTOs to domain entities
  - Performs bulk inserts
  - Handles logging and error scenarios

**Characteristics**:
- ✅ Depends only on Domain layer
- ✅ Defines interfaces (not implementations)
- ✅ Contains no infrastructure concerns
- ✅ Easily unit testable with mocks

### 3. Infrastructure Layer (`IPSDataAcquisitionWorker.Infrastructure`)

**Purpose**: Implements external services and data persistence.

**Key Components**:

#### Data
- `ApplicationDbContext` - EF Core DbContext implementation
  - Implements `IApplicationDbContext`
  - Configures entity relationships
  - Auto-updates `UpdatedAt` on entity changes
  - **Model-First Approach** - No migrations

#### Services
- `RabbitMqConsumerService` - Background service for consuming RabbitMQ messages
  - Connects to RabbitMQ on startup
  - Declares and binds to queue
  - Implements QoS (prefetch count)
  - Handles message acknowledgment/rejection
  - Auto-recovery on connection failures
  - Graceful shutdown

#### Configuration
- `DependencyInjection.cs` - Service registration
  - Registers DbContext with PostgreSQL
  - Registers application services
  - Registers background services

**Characteristics**:
- ✅ Depends on Application and Domain layers
- ✅ Contains all external dependencies
- ✅ Implements interfaces defined in Application layer
- ✅ Handles all I/O operations

**Design Decisions**:
- **Model-First Database**: Does not create or manage migrations. Assumes tables exist (created by API project)
- **Auto-Recovery**: RabbitMQ connection auto-recovers on network failures
- **Scoped Services**: Creates new scope for each message to ensure proper lifetime management
- **Error Handling**: Invalid messages rejected without requeue; processing errors trigger requeue for retry

### 4. Worker Layer (`IPSDataAcquisitionWorker.Worker`)

**Purpose**: Application entry point and composition root.

**Key Components**:
- `Program.cs` - Application bootstrap and dependency injection setup
- `appsettings.json` - Configuration files for different environments

**Characteristics**:
- ✅ Depends on Infrastructure layer only
- ✅ Composes all layers
- ✅ Configures logging and hosting
- ✅ Environment-specific configuration

## Design Patterns

### 1. Dependency Inversion Principle (DIP)

High-level modules (Application) don't depend on low-level modules (Infrastructure). Both depend on abstractions (interfaces).

```csharp
// Application layer defines the interface
public interface IApplicationDbContext
{
    DbSet<IMUData> IMUData { get; set; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

// Infrastructure layer implements it
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    // Implementation
}
```

### 2. Repository Pattern (via DbContext)

EF Core's `DbContext` acts as a Unit of Work and `DbSet<T>` as repositories.

### 3. Background Service Pattern

`RabbitMqConsumerService` extends `BackgroundService` for long-running background tasks.

### 4. Factory Pattern (Service Provider)

Uses `IServiceProvider` to create scoped services for each message processing.

```csharp
using (var scope = _serviceProvider.CreateScope())
{
    var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();
    await processor.ProcessIMUDataAsync(queueMessage, stoppingToken);
}
```

### 5. Bulk Insert Pattern

Processes entire message batch before saving to database for better performance.

```csharp
await _context.IMUData.AddRangeAsync(imuDataList, cancellationToken);
await _context.SaveChangesAsync(cancellationToken);
```

## Message Flow

```
┌─────────────┐
│  RabbitMQ   │
│   Queue     │
└──────┬──────┘
       │ 1. Message arrives
       │
       ▼
┌──────────────────────────┐
│ RabbitMqConsumerService  │
│ (Infrastructure Layer)   │
└──────┬───────────────────┘
       │ 2. Deserialize JSON
       │
       ▼
┌──────────────────────────┐
│    IMUDataProcessor      │
│  (Application Layer)     │
└──────┬───────────────────┘
       │ 3. Map DTO to Entity
       │
       ▼
┌──────────────────────────┐
│   ApplicationDbContext   │
│ (Infrastructure Layer)   │
└──────┬───────────────────┘
       │ 4. Bulk Insert
       │
       ▼
┌──────────────────────────┐
│     PostgreSQL DB        │
└──────────────────────────┘
```

## Database Strategy

### Model-First Approach

This worker service uses a **Model-First** approach:

1. **Domain entities** (`IMUData`) are defined in code
2. **No migrations** are created or managed by this service
3. **Database schema** is assumed to already exist (created by the API project)
4. **DbContext** is configured to NOT create/update database

**Benefits**:
- ✅ Single source of truth for schema (API project)
- ✅ Prevents accidental schema changes
- ✅ Faster startup (no migration checks)
- ✅ Clear separation of responsibilities

**Implementation**:
```csharp
services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseSnakeCaseNamingConvention();
    // Don't ensure database is created
    // Don't run migrations
});
```

## Error Handling Strategy

### Message Processing Errors

1. **Deserialization Errors** (Invalid JSON)
   - Log error
   - Reject message without requeue
   - Prevents infinite retry loop

2. **Processing Errors** (Database issues, etc.)
   - Log error
   - Reject message with requeue
   - Allows retry after transient failures

3. **Fatal Errors** (RabbitMQ connection lost)
   - Log error
   - Service stops
   - Kubernetes/Docker will restart

### Database Errors

- Transient errors (connection timeout) → Message requeued
- Constraint violations → Message rejected (bad data)
- Deadlocks → Message requeued with exponential backoff (via RabbitMQ)

## Performance Considerations

### Bulk Insert

Instead of inserting each data point individually:
```csharp
await _context.IMUData.AddRangeAsync(imuDataList, cancellationToken);
```

**Benefits**:
- 10-50x faster than individual inserts
- Single transaction
- Reduced database round-trips

### QoS (Quality of Service)

Configurable prefetch count limits concurrent messages:
```csharp
await _channel.BasicQosAsync(0, prefetchCount, false, stoppingToken);
```

**Benefits**:
- Prevents worker overload
- Controls memory usage
- Provides backpressure to RabbitMQ

### Connection Pooling

EF Core automatically pools database connections.

## Scalability

### Horizontal Scaling

Multiple worker instances can run simultaneously:
- Each processes messages independently
- RabbitMQ distributes messages via round-robin
- No shared state between workers

### Vertical Scaling

Increase `PrefetchCount` to process more messages concurrently per worker.

### Auto-Scaling Triggers

Monitor these metrics:
- RabbitMQ queue depth
- Message processing rate
- Database connection pool usage
- CPU and memory usage

## Logging Strategy

Structured logging with different levels:

- **Information**: Normal operations (message received, processed)
- **Warning**: Recoverable issues (invalid message format)
- **Error**: Processing failures (database errors)
- **Critical**: Fatal errors (RabbitMQ connection lost)

## Testing Strategy

### Unit Tests
- Application layer services (mock `IApplicationDbContext`)
- Domain entity behavior
- DTO mapping logic

### Integration Tests
- Database operations (in-memory PostgreSQL)
- Message processing end-to-end
- RabbitMQ consumer behavior

### Load Tests
- Message throughput
- Concurrent message processing
- Database bulk insert performance

## Future Enhancements

1. **Dead Letter Queue** - Move failed messages after N retries
2. **Idempotency** - Detect and skip duplicate messages
3. **Metrics/Telemetry** - Export Prometheus metrics
4. **Circuit Breaker** - Protect against database failures
5. **Health Checks** - HTTP endpoint for liveness/readiness probes

