using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Common;
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
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.Department)
            .Include(p => p.Patient)
                .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Where(p => p.DentistUserId == dentistUserId || p.TreatmentSession.DentistUserId == dentistUserId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var k = search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.AppointmentId.ToString().Contains(k) ||
                p.Patient.FullName.ToLower().Contains(k) ||
                p.PrescriptionItems.Any(pi => pi.Medicine.MedicineName.ToLower().Contains(k)));
        }

        var prescriptions = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var dtoList = prescriptions.Select(MapToDto).ToList();
        return ApiResponse<List<PrescriptionDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<List<PrescriptionDto>>> GetPatientPrescriptionsAsync(long patientUserId, CancellationToken ct = default)
    {
        var prescriptions = await _dbContext.Prescriptions
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.Department)
            .Include(p => p.Patient)
                .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Where(p => p.PatientUserId == patientUserId)
            .OrderByDescending(p => p.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var dtoList = prescriptions.Select(MapToDto).ToList();
        return ApiResponse<List<PrescriptionDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<PrescriptionDto>> GetPrescriptionDetailAsync(long prescriptionId, CancellationToken ct = default)
    {
        var pres = await _dbContext.Prescriptions
            .Include(p => p.PrescriptionItems)
                .ThenInclude(pi => pi.Medicine)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(p => p.Dentist)
                .ThenInclude(d => d.Department)
            .Include(p => p.Patient)
                .ThenInclude(pu => pu.User)
            .Include(p => p.TreatmentSession)
                .ThenInclude(s => s.Appointment)
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
            .Include(a => a.Patient)
                .ThenInclude(pu => pu.User)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.Prescription)
            .Where(a => a.AppointmentServices.Any(s => s.AssignedDentistUserId == dentistUserId))
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .AsNoTracking()
            .ToListAsync(ct);

        var list = appointments.Select(a =>
        {
            var sessionWithPres = a.TreatmentSessions.FirstOrDefault(s => s.Prescription != null);
            var svc = a.AppointmentServices.FirstOrDefault(s => s.AssignedDentistUserId == dentistUserId) ?? a.AppointmentServices.FirstOrDefault();
            return new PrescriptionEligibleAppointmentDto
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = $"APT-{a.AppointmentId:D6}",
                PatientUserId = a.PatientUserId,
                PatientName = a.Patient.FullName,
                PatientCode = $"BN-{a.PatientUserId}",
                PatientPhone = a.Patient.User?.PhoneNumber,
                DepartmentName = svc?.DepartmentService?.Department?.Name ?? "Chuyên khoa Răng Hàm Mặt",
                ServiceName = svc?.DepartmentService?.ServiceName ?? "Khám nha khoa",
                ScheduledStart = svc?.ScheduledStart ?? a.CreatedAt,
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
            .Include(a => a.Patient)
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<PrescriptionDto>.Fail("Không tìm thấy thông tin ca khám.");
        }

        var dentistSvc = appointment.AppointmentServices.FirstOrDefault(s => s.AssignedDentistUserId == dentistUserId)
                         ?? appointment.AppointmentServices.FirstOrDefault();

        if (dentistSvc == null)
        {
            return ApiResponse<PrescriptionDto>.Fail("Ca khám này chưa có dịch vụ được chỉ định.");
        }

        if (dentistSvc.AssignedDentistUserId.HasValue && dentistSvc.AssignedDentistUserId.Value != dentistUserId)
        {
            return ApiResponse<PrescriptionDto>.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");
        }

        if (appointment.Status != AppointmentStatus.InProgress && appointment.Status != AppointmentStatus.InService && appointment.Status != AppointmentStatus.Completed)
        {
            return ApiResponse<PrescriptionDto>.Fail($"Bác sĩ chỉ có thể kê đơn thuốc sau khi ca khám đã bắt đầu diễn ra hoặc đã hoàn tất khám (Trạng thái hiện tại: {appointment.Status}).");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        if (_dbContext.Database.IsRelational())
        {
            tx = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            var session = await _dbContext.TreatmentSessions
                .Include(s => s.Prescription)
                    .ThenInclude(p => p!.PrescriptionItems)
                .FirstOrDefaultAsync(s => s.AppointmentId == request.AppointmentId, ct);

            if (session == null)
            {
                session = new TreatmentSession
                {
                    AppointmentId = appointment.AppointmentId,
                    AppointmentServiceId = dentistSvc.AppointmentServiceId,
                    PatientUserId = appointment.PatientUserId,
                    DentistUserId = dentistUserId,
                    SessionDate = ClinicTimeHelper.TodayDateOnly,
                    Notes = "Khám & Kê đơn thuốc",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.TreatmentSessions.Add(session);
                await _dbContext.SaveChangesAsync(ct);
            }

            Prescription pres;
            if (session.Prescription == null)
            {
                pres = new Prescription
                {
                    TreatmentSessionId = session.SessionId,
                    AppointmentId = appointment.AppointmentId,
                    PatientUserId = appointment.PatientUserId,
                    DentistUserId = dentistUserId,
                    Notes = request.GeneralInstructions?.Trim(),
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Prescriptions.Add(pres);
                await _dbContext.SaveChangesAsync(ct);
            }
            else
            {
                pres = session.Prescription;
                pres.Notes = request.GeneralInstructions?.Trim();
                pres.UpdatedAt = DateTime.UtcNow;

                if (pres.PrescriptionItems.Any())
                {
                    _dbContext.PrescriptionItems.RemoveRange(pres.PrescriptionItems);
                    await _dbContext.SaveChangesAsync(ct);
                }
            }

            var addedMedicineIds = new HashSet<long>();
            foreach (var item in request.Items.Where(i => !string.IsNullOrWhiteSpace(i.MedicationName)))
            {
                var medName = item.MedicationName.Trim();
                var med = await _dbContext.Medicines
                    .FirstOrDefaultAsync(m => m.MedicineName.ToLower() == medName.ToLower(), ct);

                if (med == null || !med.IsActive)
                {
                    return ApiResponse<PrescriptionDto>.Fail($"Thuốc '{medName}' không tồn tại hoặc đã ngừng cung cấp trong danh mục dược.");
                }

                if (addedMedicineIds.Contains(med.MedicineId))
                {
                    return ApiResponse<PrescriptionDto>.Fail($"Đơn thuốc không được chứa trùng lặp cùng một loại thuốc ('{med.MedicineName}').");
                }
                addedMedicineIds.Add(med.MedicineId);

                var qty = item.Quantity > 0 ? item.Quantity : 10;

                var pItem = new PrescriptionItem
                {
                    PrescriptionId = pres.PrescriptionId,
                    AppointmentId = appointment.AppointmentId,
                    MedicineId = med.MedicineId,
                    Dosage = item.Dosage?.Trim() ?? "Theo chỉ định",
                    Frequency = item.Frequency?.Trim() ?? "Ngày 2 lần",
                    Instructions = string.IsNullOrWhiteSpace(item.Duration)
                        ? item.Instructions?.Trim()
                        : $"{item.Duration.Trim()} - {item.Instructions?.Trim()}".Trim(' ', '-'),
                    Quantity = qty,
                    QuantityDispensed = 0,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.PrescriptionItems.Add(pItem);
            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }

            var fullPres = await _dbContext.Prescriptions
                .Include(p => p.PrescriptionItems)
                    .ThenInclude(pi => pi.Medicine)
                .Include(p => p.Dentist)
                    .ThenInclude(d => d.StaffProfile)
                .Include(p => p.Dentist)
                    .ThenInclude(d => d.Department)
                .Include(p => p.Patient)
                    .ThenInclude(pu => pu.User)
                .Include(p => p.TreatmentSession)
                    .ThenInclude(s => s.Appointment)
                .FirstAsync(p => p.PrescriptionId == pres.PrescriptionId, ct);

            return ApiResponse<PrescriptionDto>.Ok(MapToDto(fullPres), "Đã kê đơn thuốc thành công!");
        }
        catch (Exception ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(ct);
            }
            _logger.LogError(ex, "Lỗi khi kê đơn thuốc cho ca khám {AppointmentId}", request.AppointmentId);
            return ApiResponse<PrescriptionDto>.Fail("Đã xảy ra lỗi trong quá trình kê đơn thuốc. Dữ liệu đã được hoàn tác an toàn.");
        }
        finally
        {
            if (tx != null)
            {
                await tx.DisposeAsync();
            }
        }
    }

    private static PrescriptionDto MapToDto(Prescription p)
    {
        var patient = p.Patient;
        var dentist = p.Dentist;
        var dept = dentist?.Department;

        int? age = null;
        if (patient?.DateOfBirth.HasValue == true)
        {
            age = DateTime.UtcNow.Year - patient.DateOfBirth.Value.Year;
        }

        return new PrescriptionDto
        {
            PrescriptionId = p.PrescriptionId,
            TreatmentSessionId = p.TreatmentSessionId,
            AppointmentId = p.AppointmentId,
            AppointmentCode = $"APT-{p.AppointmentId:D6}",
            PatientUserId = p.PatientUserId,
            PatientName = patient?.FullName ?? "Bệnh nhân",
            PatientCode = $"BN-{p.PatientUserId}",
            PatientPhone = patient?.User?.PhoneNumber,
            PatientGender = patient?.Gender,
            PatientAge = age,
            DentistUserId = p.DentistUserId,
            DentistName = dentist?.StaffProfile?.FullName ?? "Bác sĩ điều trị",
            DepartmentName = dept?.Name ?? "Chuyên khoa Răng Hàm Mặt",
            GeneralInstructions = p.Notes,
            IssuedAt = p.CreatedAt,
            Items = p.PrescriptionItems.Select(pi => new PrescriptionItemDto
            {
                PrescriptionItemId = pi.PrescriptionItemId,
                MedicationName = pi.Medicine?.MedicineName ?? "Thuốc nha khoa",
                Dosage = pi.Dosage,
                Frequency = pi.Frequency,
                Duration = pi.Instructions,
                Instructions = pi.Instructions
            }).ToList()
        };
    }
}
