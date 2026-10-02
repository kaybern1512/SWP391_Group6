using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(
        long userId,
        long? appointmentId,
        string type,
        string subject,
        string content,
        CancellationToken ct = default);
}
