using System;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(DentalClinicDbContext dbContext, ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(long? userId, string action, string entityName, string? entityId, string? oldValues = null, string? newValues = null, CancellationToken ct = default)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AuditLogs.Add(auditLog);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Logging audit log failure must never break business flow
            _logger.LogError(ex, "Failed to write audit log for action {Action} on entity {EntityName}.", action, entityName);
        }
    }
}
