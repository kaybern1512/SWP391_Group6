using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IAppointmentApiService
{
    // Booking Lookups
    Task<ApiResult<List<DepartmentDto>>> GetDepartmentsAsync();
    Task<ApiResult<List<DepartmentServiceDto>>> GetServicesAsync(long departmentId);
    Task<ApiResult<List<DentistOptionDto>>> GetDentistsAsync(long departmentId);
    Task<ApiResult<List<AvailableSlotDto>>> GetAvailableSlotsAsync(long departmentId, long serviceId, DateOnly date, long? dentistUserId = null);

    // Patient
    Task<ApiResult<AppointmentDto>> BookAppointmentAsync(CreateAppointmentRequest request);
    Task<ApiResult<List<AppointmentDto>>> GetPatientAppointmentsAsync(string? status = null);
    Task<ApiResult<AppointmentDto>> GetAppointmentDetailAsync(long id);
    Task<ApiResult> PatientWithdrawAsync(long id, string? reason = null);
    Task<ApiResult> PatientCancelAsync(long id, string reason);
    Task<ApiResult<AppointmentChangeProposalDto>> GetProposalAsync(long id);
    Task<ApiResult> AcceptProposalAsync(long id);
    Task<ApiResult> RejectProposalAsync(long id);

    // Receptionist
    Task<ApiResult<List<AppointmentDto>>> GetReceptionistRequestsAsync();
    Task<ApiResult> ReceptionistForwardAsync(long id, string? note = null);
    Task<ApiResult> ReceptionistRejectAsync(long id, string reason);
    Task<ApiResult<List<AppointmentDto>>> GetReceptionistTodayConfirmedAsync(string? search = null);
    Task<ApiResult> ReceptionistCheckInAsync(long id, string? note = null);

    // Department Manager
    Task<ApiResult<List<AppointmentDto>>> GetDepartmentRequestsAsync();
    Task<ApiResult<List<DentistOptionDto>>> GetAvailableDentistsForAppointmentAsync(long id);
    Task<ApiResult<AvailableResourcesDto>> GetAvailableResourcesAsync(long departmentId, DateTime start, DateTime end);
    Task<ApiResult<AppointmentDto>> ManagerConfirmAsync(long id, ManagerConfirmRequest request);
    Task<ApiResult> ManagerProposeChangeAsync(long id, ManagerProposeChangeRequest request);

    // Dentist
    Task<ApiResult<List<AppointmentDto>>> GetDentistTodayAppointmentsAsync();
}
