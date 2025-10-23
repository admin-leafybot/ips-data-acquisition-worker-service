# syntax=docker/dockerfile:1.7-labs

# -------------------- Build stage --------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files first to leverage Docker layer caching
COPY ./IPSDataAcquisitionWorker.sln ./
COPY ./src/IPSDataAcquisitionWorker.Domain/IPSDataAcquisitionWorker.Domain.csproj ./src/IPSDataAcquisitionWorker.Domain/
COPY ./src/IPSDataAcquisitionWorker.Application/IPSDataAcquisitionWorker.Application.csproj ./src/IPSDataAcquisitionWorker.Application/
COPY ./src/IPSDataAcquisitionWorker.Infrastructure/IPSDataAcquisitionWorker.Infrastructure.csproj ./src/IPSDataAcquisitionWorker.Infrastructure/
COPY ./src/IPSDataAcquisitionWorker.Worker/IPSDataAcquisitionWorker.Worker.csproj ./src/IPSDataAcquisitionWorker.Worker/

# Restore
RUN dotnet restore ./IPSDataAcquisitionWorker.sln

# Copy the rest of the source
COPY ./src ./src

# Publish (Release)
RUN dotnet publish ./src/IPSDataAcquisitionWorker.Worker/IPSDataAcquisitionWorker.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

# -------------------- Runtime stage --------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Set environment defaults
ENV DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IPSDataAcquisitionWorker.Worker.dll"]

