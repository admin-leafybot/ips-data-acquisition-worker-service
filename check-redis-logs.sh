#!/bin/bash
echo "=== Checking Worker Service Logs for Redis Connection ==="
echo ""
echo "Looking for Redis connection messages..."
docker logs ips-data-acquisition-worker 2>&1 | grep -i redis | tail -20
echo ""
echo "=== Recent logs (last 50 lines) ==="
docker logs --tail 50 ips-data-acquisition-worker
