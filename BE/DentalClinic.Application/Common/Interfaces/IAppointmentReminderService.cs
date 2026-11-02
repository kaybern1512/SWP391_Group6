using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAppointmentReminderService
{
    /// <summary>
    /// Tạo 2 lịch gửi email nhắc hẹn tự động T-24h và T-8h khi lịch hẹn chuyển sang Confirmed.
    /// </summary>
    Task ScheduleRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Hủy các job nhắc hẹn khi lịch hẹn bị hủy (Cancelled / Withdrawn).
    /// </summary>
    Task CancelRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Hủy các job nhắc cũ và lập lại các job nhắc mới khi lịch hẹn được dời (Rescheduled).
    /// </summary>
    Task RescheduleRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default);

    /// <summary>
    /// Worker định kỳ quét và phát đi các email nhắc hẹn đến hạn.
    /// </summary>
    Task<int> ProcessDueRemindersAsync(CancellationToken ct = default);
}
