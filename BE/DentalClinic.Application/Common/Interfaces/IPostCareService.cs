using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IPostCareService
{
    // --- Department Referrals ---
    Task<ApiResponse<DepartmentReferralDto>> CreateReferralAsync(
        long dentistUserId,
        CreateDepartmentReferralRequest request,
        CancellationToken ct = default);

    Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(
        long managerUserId,
        long? departmentId = null,
        string? status = null,
        CancellationToken ct = default);

    Task<ApiResponse<List<DepartmentReferralDto>>> GetDentistReferralsAsync(
        long dentistUserId,
        CancellationToken ct = default);

    Task<ApiResponse<DepartmentReferralDto>> GetReferralByIdAsync(
        long userId,
        string role,
        long referralId,
        CancellationToken ct = default);

    Task<ApiResponse<DepartmentReferralDto>> ReviewReferralAsync(
        long managerUserId,
        long referralId,
        ReviewDepartmentReferralRequest request,
        CancellationToken ct = default);

    Task<ApiResponse<List<DepartmentDentistLookupDto>>> GetDepartmentDentistsAsync(
        long managerUserId,
        long? departmentId = null,
        CancellationToken ct = default);

    // --- Follow-Up Schedules ---
    Task<ApiResponse<FollowUpScheduleDto>> CreateFollowUpScheduleAsync(
        long dentistUserId,
        CreateFollowUpScheduleRequest request,
        CancellationToken ct = default);

    Task<ApiResponse<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(
        long patientUserId,
        CancellationToken ct = default);

    Task<ApiResponse<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync(
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    Task<ApiResponse<FollowUpScheduleDto>> UpdateFollowUpStatusAsync(
        long userId,
        string role,
        long followUpId,
        UpdateFollowUpStatusRequest request,
        CancellationToken ct = default);
}
