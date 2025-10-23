using IPSDataAcquisitionWorker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IPSDataAcquisitionWorker.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<IMUData> IMUData { get; set; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

