# AWS Deployment Guide

This guide covers deploying the IPS Data Acquisition Worker Service to AWS using ECR (Elastic Container Registry) and EC2 with Docker Compose.

## Architecture Overview

```
┌─────────────┐      ┌──────────────┐      ┌──────────────┐
│   GitHub    │─────▶│   AWS ECR    │─────▶│   AWS EC2    │
│  Repository │      │ (Container   │      │  (Docker     │
│             │      │  Registry)   │      │   Compose)   │
└─────────────┘      └──────────────┘      └──────────────┘
                                                    │
                                                    ▼
                                            ┌───────────────┐
                                            │  PostgreSQL   │
                                            │   RabbitMQ    │
                                            └───────────────┘
```

## Prerequisites

### 1. AWS Services Setup

- ✅ AWS Account with appropriate permissions
- ✅ AWS CLI installed and configured
- ✅ ECR repository created for worker service
- ✅ EC2 instance running (Ubuntu 22.04 recommended)
- ✅ RDS PostgreSQL database (or PostgreSQL on EC2)
- ✅ Amazon MQ (RabbitMQ) or RabbitMQ on EC2
- ✅ Security groups configured

### 2. GitHub Repository Setup

- ✅ Repository secrets configured (see below)
- ✅ GitHub Actions enabled

### 3. EC2 Instance Requirements

```bash
# Minimum specs
- Instance Type: t3.medium or larger
- OS: Ubuntu 22.04 LTS
- Storage: 30GB+ GP3
- Docker installed
- Docker Compose installed
- AWS CLI installed
```

## Step 1: Create ECR Repository

```bash
# Login to AWS CLI
aws configure

# Create ECR repository for worker
aws ecr create-repository \
  --repository-name ips-worker \
  --region ap-south-1 \
  --image-scanning-configuration scanOnPush=true

# Note the repository URI from output
# Example: 123456789012.dkr.ecr.ap-south-1.amazonaws.com/ips-worker
```

## Step 2: Setup EC2 Instance

### Install Docker & Docker Compose

```bash
# SSH into EC2 instance
ssh -i your-key.pem ubuntu@<ec2-public-ip>

# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker ubuntu

# Install Docker Compose
sudo apt install docker-compose-plugin -y

# Verify installations
docker --version
docker compose version

# Install AWS CLI (if not already installed)
sudo apt install awscli -y
aws --version

# Configure AWS CLI (use IAM role or access keys)
aws configure
```

### Configure Security Groups

Ensure your EC2 security group allows:
- SSH (port 22) from your IP
- Database access (port 5432) if PostgreSQL is on same VPC
- RabbitMQ access (port 5672) if RabbitMQ is on same VPC

## Step 3: Configure GitHub Secrets

Go to your GitHub repository → Settings → Secrets and variables → Actions

### Required Secrets

| Secret Name | Description | Example |
|------------|-------------|---------|
| `AWS_REGION` | AWS region | `ap-south-1` |
| `AWS_ACCOUNT_ID` | Your AWS account ID | `123456789012` |
| `ECR_REPOSITORY_WORKER` | ECR repository name | `ips-worker` |
| `DB_CONNECTION_STRING` | PostgreSQL connection string | `Host=xxx.rds.amazonaws.com;Port=5432;Database=ips_data_acquisition;Username=admin;Password=xxx` |
| `RABBITMQ_HOST` | RabbitMQ host | `b-xxx.mq.ap-south-1.amazonaws.com` |
| `RABBITMQ_USER` | RabbitMQ username | `admin` |
| `RABBITMQ_PASSWORD` | RabbitMQ password | `SecurePassword123` |
| `EC2_HOST` | EC2 public IP or hostname | `13.126.xxx.xxx` |
| `EC2_USER` | EC2 SSH username | `ubuntu` |
| `EC2_SSH_KEY` | EC2 SSH private key (full contents) | `-----BEGIN RSA PRIVATE KEY-----\n...` |
| `AWS_ACCESS_KEY_ID` | AWS access key for deployment | `AKIAIOSFODNN7EXAMPLE` |
| `AWS_SECRET_ACCESS_KEY` | AWS secret key | `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY` |

### How to Get Values

#### EC2_SSH_KEY
```bash
# On your local machine, copy entire private key content
cat ~/.ssh/your-key.pem
# Copy everything including BEGIN and END lines
```

#### AWS Credentials
Create an IAM user with these permissions:
- `AmazonEC2ContainerRegistryFullAccess`
- `AmazonSSMReadOnlyAccess` (if using Parameter Store)

```bash
# Create IAM user and get credentials
aws iam create-user --user-name github-deployer
aws iam attach-user-policy --user-name github-deployer \
  --policy-arn arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryFullAccess
aws iam create-access-key --user-name github-deployer
```

## Step 4: Deploy Using GitHub Actions

### Automatic Deployment

Push to `main` branch to trigger deployment:

```bash
git add .
git commit -m "Deploy worker service"
git push origin main
```

### Manual Deployment

Go to GitHub → Actions → "Build, Push to ECR, Deploy to EC2" → "Run workflow"

### Deployment Steps (Automated)

1. **Checkout code**
2. **Replace placeholders** in appsettings with secrets
3. **Build Docker image**
4. **Push to ECR**
5. **SSH to EC2**
6. **Pull latest image**
7. **Deploy with docker-compose**

## Step 5: Manual Deployment (Alternative)

If you prefer to deploy manually without GitHub Actions:

### Build and Push to ECR

```bash
# On your local machine

# Login to ECR
aws ecr get-login-password --region ap-south-1 | \
  docker login --username AWS --password-stdin \
  123456789012.dkr.ecr.ap-south-1.amazonaws.com

# Build image
docker build -t ips-worker:latest .

# Tag for ECR
docker tag ips-worker:latest \
  123456789012.dkr.ecr.ap-south-1.amazonaws.com/ips-worker:latest

# Push to ECR
docker push 123456789012.dkr.ecr.ap-south-1.amazonaws.com/ips-worker:latest
```

### Deploy on EC2

```bash
# SSH to EC2
ssh -i your-key.pem ubuntu@<ec2-public-ip>

# Create deployment directory
mkdir -p ~/ips-data-acquisition-worker
cd ~/ips-data-acquisition-worker

# Create docker-compose.prod.yml
cat > docker-compose.prod.yml << 'EOF'
version: "3.9"

services:
  worker:
    image: 123456789012.dkr.ecr.ap-south-1.amazonaws.com/ips-worker:latest
    container_name: ips-data-acquisition-worker
    environment:
      ConnectionStrings__Default: "Host=xxx.rds.amazonaws.com;Port=5432;Database=ips_data_acquisition;Username=admin;Password=xxx"
      RabbitMQ__HostName: "b-xxx.mq.ap-south-1.amazonaws.com"
      RabbitMQ__Port: 5672
      RabbitMQ__UserName: "admin"
      RabbitMQ__Password: "password"
      RabbitMQ__QueueName: "imu-data-queue"
      RabbitMQ__PrefetchCount: 10
    restart: unless-stopped
    logging:
      driver: "awslogs"
      options:
        awslogs-region: "ap-south-1"
        awslogs-group: "/ecs/ips-worker"
        awslogs-stream: "ips-worker-container"
        awslogs-create-group: "true"
EOF

# Login to ECR
aws ecr get-login-password --region ap-south-1 | \
  sudo docker login --username AWS --password-stdin \
  123456789012.dkr.ecr.ap-south-1.amazonaws.com

# Pull and start
sudo docker-compose -f docker-compose.prod.yml pull
sudo docker-compose -f docker-compose.prod.yml up -d

# Verify
sudo docker-compose -f docker-compose.prod.yml ps
sudo docker-compose -f docker-compose.prod.yml logs -f
```

## Step 6: Monitoring

### View Logs

```bash
# On EC2
cd ~/ips-data-acquisition-worker
sudo docker-compose -f docker-compose.prod.yml logs -f worker

# Or view Docker logs directly
sudo docker logs -f ips-data-acquisition-worker
```

### CloudWatch Logs

If you configured CloudWatch logging (awslogs driver), view logs in AWS Console:
1. Go to CloudWatch → Log groups
2. Select `/ecs/ips-worker`
3. View log streams

### Health Check

```bash
# Check if container is running
sudo docker ps | grep ips-worker

# Check resource usage
sudo docker stats ips-data-acquisition-worker

# Check RabbitMQ queue
# Access RabbitMQ Management UI at http://<rabbitmq-host>:15672
```

## Step 7: Scaling

### Vertical Scaling

Update `PrefetchCount` to process more messages per worker:

```yaml
environment:
  RabbitMQ__PrefetchCount: 20  # Increased from 10
```

### Horizontal Scaling

Run multiple worker instances:

**Option 1: Multiple containers on same EC2**
```yaml
services:
  worker-1:
    image: ...
    container_name: ips-worker-1
    ...
  
  worker-2:
    image: ...
    container_name: ips-worker-2
    ...
```

**Option 2: Multiple EC2 instances**
- Launch additional EC2 instances
- Deploy worker on each using same steps
- RabbitMQ will distribute messages across all workers

**Option 3: ECS (Recommended for Production)**

See "ECS Deployment" section below.

## ECS Deployment (Recommended for Production)

For better scalability and management, deploy using ECS:

### Create ECS Cluster

```bash
aws ecs create-cluster \
  --cluster-name ips-worker-cluster \
  --region ap-south-1
```

### Create Task Definition

```json
{
  "family": "ips-worker",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "containerDefinitions": [
    {
      "name": "ips-worker",
      "image": "123456789012.dkr.ecr.ap-south-1.amazonaws.com/ips-worker:latest",
      "essential": true,
      "environment": [
        {"name": "ConnectionStrings__Default", "value": "Host=xxx;..."},
        {"name": "RabbitMQ__HostName", "value": "b-xxx.mq.ap-south-1.amazonaws.com"}
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/ips-worker",
          "awslogs-region": "ap-south-1",
          "awslogs-stream-prefix": "ecs"
        }
      }
    }
  ]
}
```

### Create ECS Service

```bash
aws ecs create-service \
  --cluster ips-worker-cluster \
  --service-name ips-worker-service \
  --task-definition ips-worker:1 \
  --desired-count 2 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[subnet-xxx],securityGroups=[sg-xxx],assignPublicIp=ENABLED}" \
  --region ap-south-1
```

### Auto-Scaling (ECS)

```bash
# Register scalable target
aws application-autoscaling register-scalable-target \
  --service-namespace ecs \
  --scalable-dimension ecs:service:DesiredCount \
  --resource-id service/ips-worker-cluster/ips-worker-service \
  --min-capacity 1 \
  --max-capacity 10 \
  --region ap-south-1

# Create scaling policy based on RabbitMQ queue depth
aws application-autoscaling put-scaling-policy \
  --policy-name rabbitmq-queue-scaling \
  --service-namespace ecs \
  --scalable-dimension ecs:service:DesiredCount \
  --resource-id service/ips-worker-cluster/ips-worker-service \
  --policy-type TargetTrackingScaling \
  --target-tracking-scaling-policy-configuration file://scaling-policy.json
```

## Troubleshooting

### Container not starting

```bash
# Check logs
sudo docker logs ips-data-acquisition-worker

# Check if database is accessible
telnet <db-host> 5432

# Check if RabbitMQ is accessible
telnet <rabbitmq-host> 5672
```

### Messages not being processed

```bash
# Verify queue name matches
echo $RabbitMQ__QueueName

# Check RabbitMQ Management UI
# Verify queue has messages

# Check worker logs for errors
sudo docker logs -f ips-data-acquisition-worker
```

### Database connection issues

```bash
# Test connection from EC2
psql "Host=xxx;Port=5432;Database=ips_data_acquisition;Username=admin;Password=xxx"

# Verify security group allows EC2 → RDS traffic
```

### ECR push failures

```bash
# Re-authenticate to ECR
aws ecr get-login-password --region ap-south-1 | \
  docker login --username AWS --password-stdin \
  123456789012.dkr.ecr.ap-south-1.amazonaws.com
```

## Cost Optimization

### EC2 Pricing
- Use Reserved Instances for 1-year commitment (save ~30%)
- Use Spot Instances for non-critical workloads (save ~70%)
- Right-size instance based on actual usage

### ECR Pricing
- First 500 MB/month free
- $0.10 per GB/month for storage
- $0.09 per GB for data transfer

### Data Transfer
- Keep worker, database, and RabbitMQ in same VPC/region
- Use VPC endpoints to avoid internet data transfer charges

### RDS/MQ Pricing
- Consider Aurora Serverless for variable workloads
- Use RabbitMQ on EC2 instead of Amazon MQ for cost savings

## Security Best Practices

1. **Use IAM Roles** instead of access keys when possible
2. **Store secrets** in AWS Secrets Manager or Parameter Store
3. **Enable encryption** for RDS and data in transit
4. **Use VPC** - Don't expose services to internet
5. **Regular updates** - Keep Docker images and EC2 updated
6. **Least privilege** - Grant minimum required permissions
7. **Monitor logs** - Enable CloudWatch for audit trails

## Backup & Disaster Recovery

### Database Backups
- Enable automated RDS backups (retained for 7-35 days)
- Take manual snapshots before major changes

### Container Rollback
```bash
# List ECR image tags
aws ecr list-images --repository-name ips-worker

# Deploy previous version
docker pull 123456789012.dkr.ecr.ap-south-1.amazonaws.com/ips-worker:previous-sha
# Update docker-compose.yml and redeploy
```

## Production Checklist

- [ ] ECR repository created with scan on push enabled
- [ ] EC2 instance launched with appropriate security groups
- [ ] RDS PostgreSQL database configured and accessible
- [ ] RabbitMQ (Amazon MQ or self-hosted) configured
- [ ] GitHub secrets configured
- [ ] Test deployment successful
- [ ] CloudWatch logs configured
- [ ] Monitoring/alerts setup
- [ ] Auto-scaling configured (if using ECS)
- [ ] Backup strategy in place
- [ ] Disaster recovery plan documented
- [ ] Cost monitoring enabled

## Support

For issues or questions:
- Check logs first
- Review GitHub Actions workflow runs
- Verify all secrets are correctly set
- Consult ARCHITECTURE.md for design details

