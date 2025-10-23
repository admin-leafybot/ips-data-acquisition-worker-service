#!/bin/bash
# Script to inspect Redis data from EC2
# Usage: ./redis-inspect.sh

REDIS_ENDPOINT="ida-redis-cache-288bkr.serverless.aps1.cache.amazonaws.com"
REDIS_PORT="6379"

echo "=== Redis Data Inspector ==="
echo ""

# Check if redis-cli is installed
if ! command -v redis-cli &> /dev/null; then
    echo "❌ redis-cli not found. Installing..."
    sudo apt update && sudo apt install redis-tools -y
fi

echo "Connecting to Redis at ${REDIS_ENDPOINT}:${REDIS_PORT}"
echo ""

# Test connection
echo "1. Testing connection..."
redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls ping
if [ $? -eq 0 ]; then
    echo "✅ Connected to Redis"
else
    echo "❌ Failed to connect"
    exit 1
fi
echo ""

# Get all keys matching pattern
echo "2. Listing all session keys (imu:session:*)..."
redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls KEYS "imu:session:*"
echo ""

# Count keys
echo "3. Total number of session keys..."
redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls KEYS "imu:session:*" | wc -l
echo ""

# Get info about a specific session
echo "4. To inspect a specific session, provide session ID:"
read -p "Enter session ID (or press Enter to skip): " SESSION_ID

if [ ! -z "$SESSION_ID" ]; then
    KEY="imu:session:${SESSION_ID}"
    echo ""
    echo "Inspecting session: ${SESSION_ID}"
    echo "Key: ${KEY}"
    echo ""
    
    # Check if key exists
    EXISTS=$(redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls EXISTS ${KEY})
    if [ "$EXISTS" -eq "1" ]; then
        echo "✅ Key exists"
        
        # Get TTL
        TTL=$(redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls TTL ${KEY})
        echo "TTL: ${TTL} seconds ($(($TTL / 3600)) hours remaining)"
        
        # Get list length
        COUNT=$(redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls LLEN ${KEY})
        echo "Data points in cache: ${COUNT}"
        
        # Get first and last data points
        echo ""
        echo "First data point (preview):"
        redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls LINDEX ${KEY} 0 | jq '.' 2>/dev/null || redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls LINDEX ${KEY} 0
        
        echo ""
        echo "Last data point (preview):"
        redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls LINDEX ${KEY} -1 | jq '.' 2>/dev/null || redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls LINDEX ${KEY} -1
        
        echo ""
        echo "To get all data points for this session:"
        echo "redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls LRANGE ${KEY} 0 -1"
    else
        echo "❌ Key does not exist"
    fi
fi

echo ""
echo "=== Interactive Redis CLI ==="
echo "To enter interactive mode, run:"
echo "redis-cli -h ${REDIS_ENDPOINT} -p ${REDIS_PORT} --tls"

