# Performance Tuning Guide

This guide helps you optimize the IPS Data Acquisition Worker Service for high-throughput scenarios.

## Your Use Case: 100 Devices @ 1000 Records Every 3 Seconds

**Expected Load:**
- 100 devices sending batches
- 1000 IMU records per batch
- Sent every 3 seconds
- **= ~33 messages/second**
- **= ~33,000 records/second**
- **= ~2,000,000 records/minute**
- **= ~120,000,000 records/hour**

## Performance Settings Explained

### 1. MaxConcurrency (Concurrent Message Processing)

**What it does:** Controls how many messages are processed simultaneously.

```json
"RabbitMQ": {
  "MaxConcurrency": 20  // Process 20 messages at the same time
}
```

**Recommendations:**
- **Development**: `5-10` (easier debugging)
- **Production**: `10-30` (based on CPU cores)
- **High Load**: `20-50` (if you have 16+ CPU cores)

**Formula:** 
```
MaxConcurrency = (Number of CPU cores × 2) - 2
Example: 8 cores → (8 × 2) - 2 = 14
```

**Trade-offs:**
- ✅ Higher = More throughput
- ⚠️ Higher = More memory usage
- ⚠️ Higher = More database connections

### 2. PrefetchCount (Queue Prefetch)

**What it does:** How many unacknowledged messages RabbitMQ delivers to the worker.

```json
"RabbitMQ": {
  "PrefetchCount": 50  // RabbitMQ delivers 50 messages ahead
}
```

**Recommendations:**
- **Development**: `10-20`
- **Production**: `30-100`
- **High Load**: `50-150`

**Formula:**
```
PrefetchCount = MaxConcurrency × 2 (to keep worker busy)
Example: MaxConcurrency 20 → PrefetchCount 40-60
```

**Trade-offs:**
- ✅ Higher = Worker always has messages to process (no idle time)
- ⚠️ Higher = More messages in memory
- ⚠️ Too high = Messages stuck in crashed worker won't requeue quickly

### 3. Database Batch Size

**What it does:** EF Core batches multiple SQL INSERT statements.

Already configured in `DependencyInjection.cs`:
```csharp
npgsqlOptions.MaxBatchSize(100);  // 100 inserts per SQL statement
```

**Recommendations:**
- **Default**: `100` (good balance)
- **High Volume**: `200-500` (faster but more memory)

### 4. Database Connection Pool

PostgreSQL connection pooling is automatic in Npgsql.

**Recommended connection string additions:**
```
Host=xxx;Port=5432;Database=xxx;Username=xxx;Password=xxx;
Minimum Pool Size=5;Maximum Pool Size=50;Connection Idle Lifetime=300;
```

**Settings:**
- `Minimum Pool Size=5` - Keep 5 connections ready
- `Maximum Pool Size=50` - Allow up to 50 concurrent connections
- `Connection Idle Lifetime=300` - Close idle connections after 5 minutes

## Configuration Examples

### Single Worker Instance (8 CPU cores)

```json
{
  "RabbitMQ": {
    "PrefetchCount": 30,
    "MaxConcurrency": 14
  }
}
```

**Expected throughput:** ~400-500 messages/sec (~400,000 records/sec)

### Single Worker Instance (16 CPU cores)

```json
{
  "RabbitMQ": {
    "PrefetchCount": 60,
    "MaxConcurrency": 30
  }
}
```

**Expected throughput:** ~800-1000 messages/sec (~800,000 records/sec)

### Multiple Worker Instances

For your load (33 messages/sec), **1 worker is sufficient**. But for scalability:

**3 Workers × 8 cores each:**
```json
{
  "RabbitMQ": {
    "PrefetchCount": 30,
    "MaxConcurrency": 14
  }
}
```

**Total throughput:** ~1,200-1,500 messages/sec (~1,200,000 records/sec)

## Scaling Strategies

### Horizontal Scaling (Recommended)

Run multiple worker instances:

**Docker Compose:**
```yaml
services:
  worker-1:
    image: ips-worker:latest
    container_name: ips-worker-1
    ...
  
  worker-2:
    image: ips-worker:latest
    container_name: ips-worker-2
    ...
  
  worker-3:
    image: ips-worker:latest
    container_name: ips-worker-3
    ...
```

**ECS (Recommended for production):**
```bash
aws ecs update-service \
  --cluster ips-worker-cluster \
  --service ips-worker-service \
  --desired-count 3
```

**Benefits:**
- ✅ RabbitMQ distributes messages across all workers (round-robin)
- ✅ If one worker crashes, others continue
- ✅ Easy to scale up/down based on load

### Vertical Scaling

Increase CPU/memory for single worker:
- t3.medium (2 vCPU, 4GB) → ~100 messages/sec
- t3.large (2 vCPU, 8GB) → ~150 messages/sec
- t3.xlarge (4 vCPU, 16GB) → ~300 messages/sec
- t3.2xlarge (8 vCPU, 32GB) → ~500 messages/sec
- c5.4xlarge (16 vCPU, 32GB) → ~1000 messages/sec

## Database Optimizations

### 1. PostgreSQL Configuration

Edit `postgresql.conf`:

```ini
# Increase write buffer
shared_buffers = 2GB               # 25% of RAM
effective_cache_size = 6GB         # 75% of RAM
maintenance_work_mem = 512MB
work_mem = 32MB

# Improve bulk insert performance
checkpoint_completion_target = 0.9
wal_buffers = 16MB
max_wal_size = 4GB
min_wal_size = 1GB

# Connection pooling
max_connections = 200

# Disable synchronous commit for better write performance (acceptable for IMU data)
synchronous_commit = off  # WARNING: Slight risk of data loss on crash
```

### 2. Table Optimizations

**Indexes** (should already exist from API project):
```sql
CREATE INDEX idx_imu_data_session_id ON imu_data(session_id);
CREATE INDEX idx_imu_data_user_id ON imu_data(user_id);
CREATE INDEX idx_imu_data_timestamp ON imu_data(timestamp);
CREATE INDEX idx_imu_data_created_at ON imu_data(created_at);
```

**Partitioning** (optional for very large datasets):
```sql
-- Partition by date for easier archival
CREATE TABLE imu_data_2024_10 PARTITION OF imu_data
FOR VALUES FROM ('2024-10-01') TO ('2024-11-01');
```

### 3. VACUUM and ANALYZE

Schedule regular maintenance:
```sql
-- Run daily
VACUUM ANALYZE imu_data;

-- Or enable autovacuum (default in PostgreSQL)
ALTER TABLE imu_data SET (autovacuum_enabled = true);
```

## Monitoring Performance

### 1. Application Logs

Monitor these metrics in logs:
```
Successfully saved 1000 IMU data points to database in 450ms (2222 records/sec)
```

**Good performance:**
- 1000 records in < 500ms (2000+ records/sec)
- No error messages
- No requeued messages

**Poor performance:**
- 1000 records in > 2000ms (< 500 records/sec)
- Frequent errors
- Many requeued messages

### 2. RabbitMQ Management UI

Access: `http://rabbitmq-host:15672`

**Monitor:**
- **Queue depth** - Should be near 0 (messages being consumed fast)
- **Message rate** - Should match incoming rate (~33/sec)
- **Unacked messages** - Should be ≤ PrefetchCount × Number of Workers

**Red flags:**
- ❌ Queue depth growing = Workers too slow
- ❌ Unacked messages > PrefetchCount × 2 = Possible stuck messages
- ❌ Message rate falling = Workers crashing or struggling

### 3. Database Performance

```sql
-- Check slow queries
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;

-- Check table size
SELECT
  schemaname,
  tablename,
  pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE tablename = 'imu_data';

-- Check index usage
SELECT
  schemaname,
  tablename,
  indexname,
  idx_scan,
  idx_tup_read,
  idx_tup_fetch
FROM pg_stat_user_indexes
WHERE tablename = 'imu_data'
ORDER BY idx_scan DESC;
```

### 4. System Metrics

Monitor EC2/ECS metrics:
- **CPU Utilization** - Should be 50-80% (room for spikes)
- **Memory Usage** - Should be < 80%
- **Network I/O** - Check for bottlenecks
- **Disk I/O** - Database writes

## Troubleshooting Performance Issues

### Issue: Messages piling up in queue

**Symptoms:**
- RabbitMQ queue depth growing
- Workers can't keep up

**Solutions:**
1. Increase `MaxConcurrency`: `10 → 20`
2. Increase `PrefetchCount`: `20 → 50`
3. Add more worker instances
4. Check database performance (slow queries)
5. Increase database connection pool size

### Issue: High CPU usage

**Symptoms:**
- Worker CPU at 100%
- Messages processed slowly

**Solutions:**
1. Reduce `MaxConcurrency` (if too high)
2. Scale horizontally (more workers, lower concurrency each)
3. Upgrade to larger instance type
4. Check for inefficient code (shouldn't be an issue)

### Issue: High memory usage

**Symptoms:**
- Worker memory at 90%+
- OOM (Out of Memory) crashes

**Solutions:**
1. Reduce `PrefetchCount`: `100 → 50`
2. Reduce `MaxConcurrency`: `30 → 15`
3. Increase instance memory
4. Check for memory leaks (rare with .NET)

### Issue: Database connection errors

**Symptoms:**
- "Too many connections" errors
- "Connection pool exhausted"

**Solutions:**
1. Increase PostgreSQL `max_connections`: `100 → 200`
2. Reduce worker `MaxConcurrency`
3. Increase connection pool: `Maximum Pool Size=50 → 100`
4. Scale workers across multiple database replicas

### Issue: Slow database writes

**Symptoms:**
- Logs show > 2000ms per batch
- CPU/Memory normal

**Solutions:**
1. Check database CPU/disk I/O
2. Add indexes (if missing)
3. Increase `shared_buffers` in PostgreSQL
4. Consider `synchronous_commit = off`
5. Use faster storage (GP3 → io2 on AWS)

## Load Testing

Before production, test with realistic load:

### 1. Simulate Message Load

```bash
# Install RabbitMQ Perf Test
docker run -it --rm pivotalrabbitmq/perf-test:latest \
  --uri amqp://guest:guest@rabbitmq-host:5672 \
  --queue imu-data-queue \
  --producers 10 \
  --consumers 0 \
  --rate 33 \
  --size 50000 \
  --flag persistent

# 10 producers × 3.3 msg/sec each = 33 msg/sec total
# size 50000 ≈ JSON message with 1000 IMU records
```

### 2. Monitor During Test

```bash
# Watch worker logs
docker logs -f ips-worker

# Watch RabbitMQ
# Access management UI at http://localhost:15672

# Watch database
psql -c "SELECT COUNT(*) FROM imu_data;"
```

### 3. Expected Results

**Good performance:**
- Queue depth stays near 0
- No error messages
- Database growing at ~33,000 records/sec
- Worker CPU 50-70%

**Need tuning:**
- Queue depth > 100 (increase MaxConcurrency)
- Worker CPU > 90% (add more workers)
- Database writes slow (optimize PostgreSQL)

## Recommended Production Setup

For your use case (100 devices, 33 messages/sec):

### Single Worker (Cost-Effective)

**EC2 Instance:** `t3.large` (2 vCPU, 8GB RAM) - $60/month

**Configuration:**
```json
{
  "RabbitMQ": {
    "PrefetchCount": 30,
    "MaxConcurrency": 10
  }
}
```

**Expected:** Handles 100-150 messages/sec (3-5x headroom)

### Multiple Workers (High Availability)

**EC2 Instances:** 2× `t3.medium` (2 vCPU, 4GB each) - $60/month

**Configuration per worker:**
```json
{
  "RabbitMQ": {
    "PrefetchCount": 20,
    "MaxConcurrency": 6
  }
}
```

**Expected:** 
- Combined: 100-150 messages/sec
- If one worker dies, other handles full load temporarily
- Better fault tolerance

### ECS Fargate (Recommended)

**Task:** 1 vCPU, 2GB RAM, Desired Count: 2-3 - $45-70/month

**Auto-scaling:** Scale 1-5 workers based on CPU or RabbitMQ queue depth

**Benefits:**
- Automatic restarts
- Auto-scaling
- No server management
- Rolling deployments

## Summary

For **100 devices @ 1000 records every 3 seconds**:

✅ **Single `t3.large` worker is sufficient**

**Recommended settings:**
- `MaxConcurrency`: 10
- `PrefetchCount`: 30
- Database connection pool: 20-30
- Monitor queue depth and CPU

**For growth (200-300 devices):**
- Scale to 2-3 workers
- Increase `MaxConcurrency` to 15-20
- Consider RDS read replicas

**For extreme growth (1000+ devices):**
- Use ECS auto-scaling (1-10 workers)
- Use Aurora PostgreSQL
- Consider sharding by device/session

