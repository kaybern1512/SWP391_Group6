using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IAttendanceApiService
{
    Task<ApiResult<List<TodayShiftAttendanceDto>>> GetTodayAttendanceAsync();
    Task<ApiResult<StaffAttendanceRecordDto>> ClockInAsync(ClockInRequest request);
    Task<ApiResult<StaffAttendanceRecordDto>> ClockOutAsync(ClockOutRequest request);
    Task<ApiResult<List<StaffAttendanceRecordDto>>> GetAttendanceHistoryAsync(DateOnly? from = null, DateOnly? to = null);
    Task<ApiResult<AttendanceCorrectionRequestDto>> SubmitCorrectionRequestAsync(SubmitAttendanceCorrectionRequest request);
    Task<ApiResult<List<AttendanceCorrectionRequestDto>>> GetMyCorrectionsAsync();
    Task<ApiResult<List<AttendanceCorrectionRequestDto>>> GetPendingCorrectionsAsync();
    Task<ApiResult<AttendanceCorrectionRequestDto>> ReviewCorrectionAsync(long id, bool approve, string? note);
}
