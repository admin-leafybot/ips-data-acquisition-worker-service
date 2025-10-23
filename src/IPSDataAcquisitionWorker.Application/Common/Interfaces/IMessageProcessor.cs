using IPSDataAcquisitionWorker.Application.Common.DTOs;

namespace IPSDataAcquisitionWorker.Application.Common.Interfaces;

public interface IMessageProcessor
{
    Task ProcessIMUDataAsync(IMUDataQueueMessage message, CancellationToken cancellationToken = default);
}

