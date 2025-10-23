# Redis Cache Usage Guide

The worker service caches IMU data points in Redis for fast session-based retrieval.

## Overview

**Architecture:**
```
RabbitMQ Message → Worker → Redis Cache (by session_id) + PostgreSQL (permanent storage)
```

**Key Structure:**
```
imu:session:{session_id} → List of IMUDataPointDto (JSON)
```

## How It Works

### 1. Data Flow

When the worker receives a message:

1. ✅ **Cache in Redis** - Appends data points to session's list
2. ✅ **Save to PostgreSQL** - Permanent storage
3. ✅ **Set Expiration** - Auto-expires after 24 hours (configurable)

### 2. Redis Data Structure

**Key Format:**
```
imu:session:550e8400-e29b-41d4-a716-446655440000
```

**Value:** Redis List containing JSON-serialized `IMUDataPointDto` objects

**Example:**
```redis
Key: imu:session:abc-123
Value: [
  {timestamp: 1234567890, accelX: 0.5, ...},
  {timestamp: 1234567891, accelX: 0.6, ...},
  ...
]
TTL: 86400 seconds (24 hours)
```

### 3. Operations

**Append Data Points:**
```csharp
await _redisCache.AppendSessionDataAsync(sessionId, dataPoints, cancellationToken);
```

**Retrieve Session Data:**
```csharp
var dataPoints = await _redisCache.GetSessionDataAsync(sessionId, cancellationToken);
```

**Get Count:**
```csharp
var count = await _redisCache.GetSessionDataCountAsync(sessionId, cancellationToken);
```

**Delete Session:**
```csharp
await _redisCache.DeleteSessionDataAsync(sessionId, cancellationToken);
```

**Set Custom Expiration:**
```csharp
await _redisCache.SetSessionExpirationAsync(sessionId, TimeSpan.FromHours(48), cancellationToken);
```

## Configuration

### appsettings.json

```json
{
  "Redis": {
    "Endpoint": "localhost:6379",
    "UseSsl": false,
    "Database": 0,
    "KeyPrefix": "imu:session:",
    "ExpirationHours": 24
  }
}
```

### Configuration Options

| Setting | Description | Default | Example |
|---------|-------------|---------|---------|
| `Endpoint` | Redis server endpoint | Required | `localhost:6379` |
| `UseSsl` | Use SSL/TLS | `true` | `false` (local), `true` (AWS) |
| `Database` | Redis database number | `0` | `0-15` |
| `KeyPrefix` | Prefix for all keys | `imu:session:` | Custom prefix |
| `ExpirationHours` | Auto-expire time | `24` | Hours before deletion |

### AWS ElastiCache Serverless

**Production Configuration:**
```json
{
  "Redis": {
    "Endpoint": "clustercfg.my-redis-cluster.abc123.memorydb.us-east-1.amazonaws.com:6379",
    "UseSsl": true,
    "Database": 0,
    "KeyPrefix": "imu:session:",
    "ExpirationHours": 24
  }
}
```

**GitHub Secret:**
```bash
REDIS_ENDPOINT=clustercfg.my-redis-cluster.abc123.memorydb.us-east-1.amazonaws.com:6379
```

## Use Cases

### 1. Real-time Session Monitoring

**Scenario:** View current session data without querying PostgreSQL

```csharp
// Get all data points for active session
var sessionData = await _redisCache.GetSessionDataAsync("session-123");

// Check how many points collected so far
var count = await _redisCache.GetSessionDataCountAsync("session-123");
```

### 2. Session Analytics

**Scenario:** Quick stats on active sessions

```redis
# Count data points
LLEN imu:session:abc-123

# Get first 10 points
LRANGE imu:session:abc-123 0 9

# Get last 10 points  
LRANGE imu:session:abc-123 -10 -1
```

### 3. Data Replay

**Scenario:** Re-process session data without hitting database

```csharp
// Get cached data for replay/analysis
var cachedData = await _redisCache.GetSessionDataAsync(sessionId);
if (cachedData != null)
{
    // Process data points...
}
```

### 4. Session Cleanup

**Scenario:** Manually clean up completed sessions

```csharp
// After session ends, delete from cache
await _redisCache.DeleteSessionDataAsync(sessionId);
```

## Performance Characteristics

### Storage

**Per Data Point:** ~1-2 KB (JSON serialized)
**Per Session (1000 points):** ~1-2 MB
**For 100 concurrent sessions:** ~100-200 MB

### Speed

- **Append:** < 1ms per operation
- **Retrieve:** < 10ms for 1000 points
- **Count:** < 1ms

### Memory Optimization

**Auto-expiration** cleans up old sessions:
- Default: 24 hours
- After expiration, data is automatically deleted
- No manual cleanup needed

## Redis Commands (Manual Operations)

### View Session Data

```redis
# List all session keys
KEYS imu:session:*

# Get session data count
LLEN imu:session:abc-123

# View first data point
LINDEX imu:session:abc-123 0

# Get all data points (use carefully with large lists!)
LRANGE imu:session:abc-123 0 -1

# Check TTL (time to live)
TTL imu:session:abc-123
```

### Manual Management

```redis
# Set custom expiration (seconds)
EXPIRE imu:session:abc-123 3600

# Delete session
DEL imu:session:abc-123

# Delete all session keys
EVAL "return redis.call('del', unpack(redis.call('keys', 'imu:session:*')))" 0
```

### Monitoring

```redis
# Get info
INFO memory
INFO stats

# Monitor commands in real-time
MONITOR

# Check memory usage of a key
MEMORY USAGE imu:session:abc-123
```

## Local Development

### Using Docker Compose

```bash
# Redis is included in docker-compose.yml
docker-compose up -d

# Access Redis CLI
docker exec -it ips-redis redis-cli

# View cached sessions
KEYS imu:session:*
```

### Using Local Redis

```bash
# Install Redis
brew install redis  # macOS
sudo apt install redis  # Ubuntu

# Start Redis
redis-server

# Connect
redis-cli
```

## Production Deployment (AWS)

### 1. Create ElastiCache Serverless

```bash
# Using AWS CLI
aws elasticache create-serverless-cache \
  --serverless-cache-name ips-redis-cache \
  --engine redis \
  --security-group-ids sg-xxx \
  --subnet-ids subnet-xxx subnet-yyy

# Get endpoint
aws elasticache describe-serverless-caches \
  --serverless-cache-name ips-redis-cache
```

### 2. Configure Security Group

Allow inbound traffic from EC2 security group:
- Type: Custom TCP
- Port: 6379
- Source: EC2 security group

### 3. Update GitHub Secrets

```bash
REDIS_ENDPOINT=clustercfg.ips-redis-cache.abc123.use1.cache.amazonaws.com:6379
```

### 4. Deploy

```bash
git add .
git commit -m "Add Redis caching"
git push origin main
```

## Monitoring & Alerts

### CloudWatch Metrics (AWS ElastiCache)

Monitor these metrics:
- **CacheHits** - Successful reads
- **CacheMisses** - Failed reads
- **BytesUsedForCache** - Memory usage
- **NetworkBytesIn/Out** - Traffic
- **EngineCPUUtilization** - CPU usage

### Application Logs

Look for:
```
✅ Successfully connected to Redis
✅ Cached 1000 data points for session abc-123
❌ Error appending data to Redis for session abc-123
```

## Troubleshooting

### Issue: Worker can't connect to Redis

**Check:**
1. Endpoint is correct
2. Security group allows traffic
3. SSL enabled if using AWS ElastiCache

```bash
# Test connection from EC2
telnet your-redis-endpoint 6379

# Or using redis-cli
redis-cli -h your-redis-endpoint -p 6379 --tls ping
```

### Issue: High memory usage

**Solutions:**
1. Reduce `ExpirationHours` (24 → 12)
2. Manually clean up old sessions
3. Upgrade Redis instance size

```redis
# Check memory usage
INFO memory

# Get largest keys
redis-cli --bigkeys
```

### Issue: Redis cache failures

**Note:** Redis failures won't stop processing!
- Data still saves to PostgreSQL
- Error logged but worker continues
- Redis cache is optional (best-effort)

## Benefits

✅ **Fast Session Queries** - Sub-10ms retrieval vs database queries
✅ **Reduced DB Load** - Real-time data cached, not in PostgreSQL
✅ **Session Analytics** - Quick stats without complex queries
✅ **Auto-Cleanup** - Expired sessions automatically deleted
✅ **Fault Tolerant** - Redis failures don't stop processing
✅ **Scalable** - Redis Serverless auto-scales

## Cost Optimization

### AWS ElastiCache Serverless Pricing

**Charges:**
- Data storage: $0.125/GB-hour
- Data processing: $0.125/GB

**Estimated Cost (100 sessions):**
- Storage: ~200 MB = $0.025/hour = $18/month
- Processing: ~10 GB/day = ~$37/month
- **Total: ~$55/month**

### Tips to Reduce Cost

1. **Lower expiration time** (24h → 12h)
2. **Cleanup completed sessions** immediately
3. **Use smaller key prefix** to reduce memory
4. **Compress JSON** if needed (future optimization)

## Summary

Redis caching provides:
- ⚡ Fast session-based data access
- 🔍 Real-time monitoring capabilities
- 💾 Automatic memory management
- 🛡️ Fault-tolerant (optional caching)
- 📈 Scalable architecture

Perfect for your use case of tracking 100 devices with real-time session data! 🚀

