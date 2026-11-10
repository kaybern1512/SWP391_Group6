using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAttendanceService
{
    Task<ApiResponse<List<TodayShiftAttendanceDto>>> GetTodayShiftsAndAttendanceAsync(long userId, string role, CancellationToken ct = default);
    Task<ApiResponse<StaffAttendanceRecordDto>> ClockInAsync(long userId, string role, ClockInRequest request, CancellationToken ct = default);
    Task<ApiResponse<StaffAttendanceRecordDto>> ClockOutAsync(long userId, string role, ClockOutRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<StaffAttendanceRecordDto>>> GetPersonalAttendanceHistoryAsync(long userId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);
    Task<ApiResponse<AttendanceCorrectionRequestDto>> SubmitCorrectionRequestAsync(long userId, SubmitAttendanceCorrectionRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<AttendanceCorrectionRequestDto>>> GetPersonalCorrectionRequestsAsync(long userId, CancellationToken ct = default);
    Task<ApiResponse<List<AttendanceCorrectionRequestDto>>> GetPendingCorrectionRequestsAsync(CancellationToken ct = default);
    Task<ApiResponse<AttendanceCorrectionRequestDto>> ReviewCorrectionRequestAsync(long correctionRequestId, long adminUserId, ReviewAttendanceCorrectionRequest request, CancellationToken ct = default);
    Task<ApiResponse<int>> ProcessMissingClockOutSweepAsync(CancellationToken ct = default);
}
