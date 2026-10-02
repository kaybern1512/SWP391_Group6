using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAppointmentService
{
    // Patient Operations
    Task<ApiResponse<AppointmentDto>> CreatePatientAppointmentAsync(long patientUserId, CreateAppointmentRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<AppointmentDto>>> GetPatientAppointmentsAsync(long patientUserId, string? statusFilter = null, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDto>> GetAppointmentDetailAsync(long appointmentId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse> PatientWithdrawAsync(long appointmentId, long patientUserId, string? reason, CancellationToken ct = default);
    Task<ApiResponse> PatientCancelAsync(long appointmentId, long patientUserId, string reason, CancellationToken ct = default);
    Task<ApiResponse<AppointmentChangeProposalDto>> GetAppointmentProposalAsync(long appointmentId, long patientUserId, CancellationToken ct = default);
    Task<ApiResponse> PatientRespondProposalAsync(long appointmentId, long patientUserId, bool accept, CancellationToken ct = default);

    // Receptionist Operations
    Task<ApiResponse<List<AppointmentDto>>> GetReceptionistRequestsAsync(CancellationToken ct = default);
    Task<ApiResponse> ReceptionistForwardAsync(long appointmentId, long staffUserId, string? note, CancellationToken ct = default);
    Task<ApiResponse> ReceptionistRejectAsync(long appointmentId, long staffUserId, string reason, CancellationToken ct = default);
    Task<ApiResponse<List<AppointmentDto>>> GetReceptionistTodayConfirmedAsync(string? searchQuery = null, CancellationToken ct = default);
    Task<ApiResponse> ReceptionistCheckInAsync(long appointmentId, long staffUserId, string? note, CancellationToken ct = default);

    // Department Manager Operations
    Task<ApiResponse<List<AppointmentDto>>> GetDepartmentRequestsAsync(long managerUserId, CancellationToken ct = default);
    Task<ApiResponse<List<DentistOptionDto>>> GetAvailableDentistsForAppointmentAsync(long appointmentId, long managerUserId, CancellationToken ct = default);
    Task<ApiResponse<AvailableResourcesDto>> GetAvailableResourcesAsync(long departmentId, DateTime start, DateTime end, CancellationToken ct = default);
    Task<ApiResponse<AppointmentDto>> ManagerConfirmAsync(long appointmentId, long managerUserId, ManagerConfirmRequest request, CancellationToken ct = default);
    Task<ApiResponse> ManagerProposeChangeAsync(long appointmentId, long managerUserId, ManagerProposeChangeRequest request, CancellationToken ct = default);

    // Dentist Operations
    Task<ApiResponse<List<AppointmentDto>>> GetDentistTodayAppointmentsAsync(long dentistUserId, CancellationToken ct = default);
}
