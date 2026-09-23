using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task LogAsync(long? userId, string action, string entityName, string? entityId, string? oldValues = null, string? newValues = null, CancellationToken ct = default);
}
