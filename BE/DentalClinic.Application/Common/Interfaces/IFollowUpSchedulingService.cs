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

    /// <summary>
    /// Bác sĩ đặt lịch hẹn tái khám trực tiếp cho bệnh nhân trong transaction an toàn,
    /// đồng thời liên kết hoặc tạo FollowUpSchedule và gửi thông báo cho bệnh nhân.
    /// </summary>
    Task<ApiResponse<FollowUpAppointmentDto>> CreateDoctorFollowUpAppointmentAsync(
        long doctorUserId,
        CreateFollowUpAppointmentRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Bệnh nhân gửi yêu cầu đổi lịch (Reschedule) hoặc đặt lại lịch (Rebook) cho cuộc hẹn tái khám.
    /// </summary>
    Task<ApiResponse<FollowUpProposalDto>> SubmitPatientChangeProposalAsync(
        long patientUserId,
        PatientFollowUpChangeRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Bệnh nhân rút yêu cầu thay đổi lịch đang chờ duyệt.
    /// </summary>
    Task<ApiResponse<bool>> WithdrawPatientProposalAsync(
        long patientUserId,
        long proposalId,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách các yêu cầu thay đổi lịch của bệnh nhân.
    /// </summary>
    Task<ApiResponse<List<FollowUpProposalDto>>> GetPatientProposalsAsync(
        long patientUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Bác sĩ lấy danh sách các yêu cầu thay đổi lịch của bệnh nhân cần phê duyệt.
    /// </summary>
    Task<ApiResponse<List<FollowUpProposalDto>>> GetDoctorPendingProposalsAsync(
        long doctorUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Bác sĩ phê duyệt hoặc từ chối yêu cầu đổi lịch / đặt lại lịch trong transaction an toàn.
    /// </summary>
    Task<ApiResponse<FollowUpProposalDto>> ResolveProposalAsync(
        long doctorUserId,
        ResolveProposalRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Lễ tân lấy danh sách lịch tái khám cần nhắc với các bộ lọc ngày và trạng thái gọi.
    /// </summary>
    Task<ApiResponse<List<FollowUpReminderDto>>> GetReceptionistRemindersAsync(
        string? dateFilter = null,
        string? callStatus = null,
        CancellationToken ct = default);

    /// <summary>
    /// Lễ tân cập nhật trạng thái gọi nhắc (NotCalled, Called, Unreachable) và ghi nhận nhật ký cuộc gọi.
    /// </summary>
    Task<ApiResponse<FollowUpReminderDto>> UpdateReminderCallStatusAsync(
        long receptionistUserId,
        UpdateReminderCallStatusRequest request,
        CancellationToken ct = default);
}
