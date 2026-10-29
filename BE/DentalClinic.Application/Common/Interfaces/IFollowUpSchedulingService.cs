using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.FollowUp.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IFollowUpSchedulingService
{
    /// <summary>
    /// Kiểm tra tính hợp lệ toàn diện của khung giờ hẹn tái khám:
    /// - Không ở quá khứ, không quá sát hiện tại (tối thiểu 30 phút).
    /// - Bác sĩ có ca làm việc bao phủ trọn vẹn khung giờ hẹn.
    /// - Không vi phạm giờ nghỉ trưa / khoảng trống giữa các ca.
    /// - Bác sĩ không nghỉ phép / bận (DentistAvailability).
    /// - Không trùng lịch với các cuộc hẹn đang hoạt động của Bác sĩ.
    /// - Không trùng lịch với các cuộc hẹn đang hoạt động của Bệnh nhân.
    /// </summary>
    Task<SchedulingValidationResult> ValidateFollowUpSlotAsync(
        long dentistUserId,
        long? patientUserId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách khung giờ tái khám còn trống của bác sĩ theo ngày và thời lượng yêu cầu.
    /// </summary>
    Task<ApiResponse<List<FollowUpSlotDto>>> GetDoctorAvailableSlotsAsync(
        long dentistUserId,
        DateTime date,
        int durationMinutes = 30,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra xung đột thời gian với các cuộc hẹn của bác sĩ: startA < endB && endA > startB.
    /// </summary>
    Task<bool> HasDoctorConflictAsync(
        long dentistUserId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra xung đột thời gian với các cuộc hẹn của bệnh nhân: startA < endB && endA > startB.
    /// </summary>
    Task<bool> HasPatientConflictAsync(
        long patientUserId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default);
}
