using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<PrescriptionService> _logger;

    public PrescriptionService(
        DentalClinicDbContext dbContext,
        ILogger<PrescriptionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiResponse<List<PrescriptionDto>>> GetDentistPrescriptionsAsync(long dentistUserId, string? search = null, CancellationToken ct = default)
    {
        var query = _dbContext.Prescriptions
            .Include(p => p.PrescriptionItems)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .Where(p => p.TreatmentSession.DentistUserId == dentistUserId ||
                        p.TreatmentSession.Appointment.AssignedDentistUserId == dentistUserId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var k = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.TreatmentSession.Appointment.AppointmentCode.ToLower().Contains(k) ||
                p.TreatmentSession.Appointment.PatientUser.FullName.ToLower().Contains(k) ||
                p.TreatmentSession.Appointment.PatientUser.PatientCode.ToLower().Contains(k) ||
                p.PrescriptionItems.Any(pi => pi.MedicationName.ToLower().Contains(k)));
        }

        var prescriptions = await query
            .OrderByDescending(p => p.IssuedAt)
            .ToListAsync(ct);

        var dtoList = prescriptions.Select(MapToDto).ToList();
        return ApiResponse<List<PrescriptionDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<List<PrescriptionDto>>> GetPatientPrescriptionsAsync(long patientUserId, CancellationToken ct = default)
    {
        var prescriptions = await _dbContext.Prescriptions
            .Include(p => p.PrescriptionItems)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .Where(p => p.TreatmentSession.Appointment.PatientUserId == patientUserId)
            .OrderByDescending(p => p.IssuedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtoList = prescriptions.Select(MapToDto).ToList();
        return ApiResponse<List<PrescriptionDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<PrescriptionDto>> GetPrescriptionDetailAsync(long prescriptionId, CancellationToken ct = default)
    {
        var pres = await _dbContext.Prescriptions
            .Include(p => p.PrescriptionItems)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .FirstOrDefaultAsync(p => p.PrescriptionId == prescriptionId, ct);

        if (pres == null)
        {
            return ApiResponse<PrescriptionDto>.Fail("Không tìm thấy đơn thuốc.");
        }

        return ApiResponse<PrescriptionDto>.Ok(MapToDto(pres));
    }

    public async Task<ApiResponse<List<PrescriptionEligibleAppointmentDto>>> GetEligibleAppointmentsForPrescriptionAsync(long dentistUserId, CancellationToken ct = default)
    {
        var appointments = await _dbContext.Appointments
            .Include(a => a.PatientUser)
                .ThenInclude(pu => pu.User)
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.Prescription)
            .Where(a => a.AssignedDentistUserId == dentistUserId)
            .OrderByDescending(a => a.ScheduledStart)
            .Take(50)
            .AsNoTracking()
            .ToListAsync(ct);

        var list = appointments.Select(a =>
        {
            var sessionWithPres = a.TreatmentSessions.FirstOrDefault(s => s.Prescription != null);
            return new PrescriptionEligibleAppointmentDto
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = a.AppointmentCode,
                PatientUserId = a.PatientUserId,
                PatientName = a.PatientUser.FullName,
                PatientCode = a.PatientUser.PatientCode,
                PatientPhone = a.PatientUser.User?.PhoneNumber,
                DepartmentName = a.Department?.Name ?? "Chuyên khoa Răng Hàm Mặt",
                ServiceName = a.RequestedService?.ServiceName ?? "Khám nha khoa",
                ScheduledStart = a.ScheduledStart,
                Status = a.Status,
                HasPrescription = sessionWithPres != null,
                PrescriptionId = sessionWithPres?.Prescription?.PrescriptionId
            };
        }).ToList();

        return ApiResponse<List<PrescriptionEligibleAppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<PrescriptionDto>> CreatePrescriptionAsync(long dentistUserId, CreatePrescriptionRequest request, CancellationToken ct = default)
    {
        if (request.Items == null || !request.Items.Any(i => !string.IsNullOrWhiteSpace(i.MedicationName)))
        {
            return ApiResponse<PrescriptionDto>.Fail("Đơn thuốc phải có ít nhất một loại thuốc.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.PatientUser)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<PrescriptionDto>.Fail("Không tìm thấy thông tin ca khám.");
        }

        if (appointment.Status != AppointmentStatus.InService && appointment.Status != AppointmentStatus.Completed)
        {
            return ApiResponse<PrescriptionDto>.Fail($"Bác sĩ chỉ có thể kê đơn thuốc sau khi ca khám đã bắt đầu diễn ra hoặc đã hoàn tất khám (Trạng thái hiện tại: {appointment.Status}).");
        }

        // Tìm hoặc tạo TreatmentSession cho Appointment này
        var session = await _dbContext.TreatmentSessions
            .Include(s => s.Prescription)
                .ThenInclude(p => p!.PrescriptionItems)
            .FirstOrDefaultAsync(s => s.AppointmentId == request.AppointmentId, ct);

        if (session == null)
        {
            var deptId = appointment.DepartmentId ?? 1;
            // Tìm hoặc tạo TreatmentPlan
            var plan = await _dbContext.TreatmentPlans
                .FirstOrDefaultAsync(tp => tp.PatientUserId == appointment.PatientUserId && tp.DepartmentId == deptId, ct);

            if (plan == null)
            {
                plan = new TreatmentPlan
                {
                    PatientUserId = appointment.PatientUserId,
                    DepartmentId = deptId,
                    CreatedByDentistUserId = dentistUserId,
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.TreatmentPlans.Add(plan);
                await _dbContext.SaveChangesAsync(ct);
            }

            // Tìm hoặc tạo TreatmentPlanItem
            var item = await _dbContext.TreatmentPlanItems
                .FirstOrDefaultAsync(tpi => tpi.TreatmentPlanId == plan.TreatmentPlanId, ct);

            if (item == null)
            {
                item = new TreatmentPlanItem
                {
                    TreatmentPlanId = plan.TreatmentPlanId,
                    SequenceNo = 1,
                    Status = "Completed",
                    Note = $"Đợt khám {appointment.AppointmentCode}"
                };
                _dbContext.TreatmentPlanItems.Add(item);
                await _dbContext.SaveChangesAsync(ct);
            }

            // Tạo TreatmentSession
            session = new TreatmentSession
            {
                TreatmentPlanItemId = item.TreatmentPlanItemId,
                AppointmentId = appointment.AppointmentId,
                DentistUserId = dentistUserId,
                SessionDate = DateTime.UtcNow,
                TreatmentResult = "Khám & Kê đơn thuốc"
            };
            _dbContext.TreatmentSessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);
        }

        Prescription pres;
        if (session.Prescription == null)
        {
            pres = new Prescription
            {
                TreatmentSessionId = session.TreatmentSessionId,
                GeneralInstructions = request.GeneralInstructions?.Trim(),
                IssuedAt = DateTime.UtcNow
            };
            _dbContext.Prescriptions.Add(pres);
            await _dbContext.SaveChangesAsync(ct);
        }
        else
        {
            pres = session.Prescription;
            pres.GeneralInstructions = request.GeneralInstructions?.Trim();
            pres.IssuedAt = DateTime.UtcNow;

            // Xóa các items cũ nếu có
            if (pres.PrescriptionItems.Any())
            {
                _dbContext.PrescriptionItems.RemoveRange(pres.PrescriptionItems);
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        // Thêm các thuốc vào đơn
        foreach (var item in request.Items.Where(i => !string.IsNullOrWhiteSpace(i.MedicationName)))
        {
            var pItem = new PrescriptionItem
            {
                PrescriptionId = pres.PrescriptionId,
                MedicationName = item.MedicationName.Trim(),
                Dosage = item.Dosage?.Trim(),
                Frequency = item.Frequency?.Trim(),
                Duration = item.Duration?.Trim(),
                Instructions = item.Instructions?.Trim()
            };
            _dbContext.PrescriptionItems.Add(pItem);
        }

        await _dbContext.SaveChangesAsync(ct);

        var fullPres = await _dbContext.Prescriptions
            .Include(p => p.PrescriptionItems)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .FirstAsync(p => p.PrescriptionId == pres.PrescriptionId, ct);

        return ApiResponse<PrescriptionDto>.Ok(MapToDto(fullPres), "Đã kê đơn thuốc thành công!");
    }

    private static PrescriptionDto MapToDto(Prescription p)
    {
        var patient = p.TreatmentSession?.Appointment?.PatientUser;
        var dentist = p.TreatmentSession?.DentistUser;
        var dept = p.TreatmentSession?.Appointment?.Department;

        int? age = null;
        if (patient?.DateOfBirth.HasValue == true)
        {
            age = DateTime.UtcNow.Year - patient.DateOfBirth.Value.Year;
        }

        return new PrescriptionDto
        {
            PrescriptionId = p.PrescriptionId,
            TreatmentSessionId = p.TreatmentSessionId,
            AppointmentId = p.TreatmentSession?.AppointmentId ?? 0,
            AppointmentCode = p.TreatmentSession?.Appointment?.AppointmentCode ?? string.Empty,
            PatientUserId = patient?.UserId ?? 0,
            PatientName = patient?.FullName ?? "Bệnh nhân",
            PatientCode = patient?.PatientCode ?? string.Empty,
            PatientPhone = patient?.User?.PhoneNumber,
            PatientGender = patient?.Gender,
            PatientAge = age,
            DentistUserId = dentist?.UserId ?? 0,
            DentistName = dentist?.User?.FullName ?? "Bác sĩ điều trị",
            DepartmentName = dept?.Name ?? "Chuyên khoa Răng Hàm Mặt",
            GeneralInstructions = p.GeneralInstructions,
            IssuedAt = p.IssuedAt,
            Items = p.PrescriptionItems.Select(pi => new PrescriptionItemDto
            {
                PrescriptionItemId = pi.PrescriptionItemId,
                MedicationName = pi.MedicationName,
                Dosage = pi.Dosage,
                Frequency = pi.Frequency,
                Duration = pi.Duration,
                Instructions = pi.Instructions
            }).ToList()
        };
    }
}
