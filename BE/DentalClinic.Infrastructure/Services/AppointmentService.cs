using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Hubs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IEmailService? _emailService;
    private readonly IAppointmentReminderService? _reminderService;
    private readonly ILogger<AppointmentService> _logger;
    private readonly Microsoft.AspNetCore.SignalR.IHubContext<ClinicQueueHub, IClinicQueueClient>? _queueHubContext;

    public AppointmentService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<AppointmentService> logger)
        : this(dbContext, notificationService, null, null, logger, null)
    {
    }

    public AppointmentService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        IEmailService? emailService,
        ILogger<AppointmentService> logger)
        : this(dbContext, notificationService, emailService, null, logger, null)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public AppointmentService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        IEmailService? emailService,
        IAppointmentReminderService? reminderService,
        ILogger<AppointmentService> logger,
        Microsoft.AspNetCore.SignalR.IHubContext<ClinicQueueHub, IClinicQueueClient>? queueHubContext = null)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _emailService = emailService;
        _reminderService = reminderService;
        _logger = logger;
        _queueHubContext = queueHubContext;
    }

    public async Task<ApiResponse<AppointmentDto>> CreatePatientAppointmentAsync(
        long patientUserId,
        CreateAppointmentRequest request,
        CancellationToken ct = default)
    {
        var eligibility = await GetBookingEligibilityAsync(patientUserId, ct);
        if (eligibility.Success && eligibility.Data != null && !eligibility.Data.IsEligible)
        {
            return ApiResponse<AppointmentDto>.Fail(eligibility.Data.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt lịch trực tuyến.");
        }

        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == patientUserId, ct);

        if (patient == null || patient.User.Status != AccountStatus.Active)
        {
            return ApiResponse<AppointmentDto>.Fail("Tài khoản bệnh nhân không hợp lệ hoặc chưa được kích hoạt.");
        }

        ServiceCombo? selectedCombo = null;
        if (request.ComboId.HasValue && request.ComboId.Value > 0)
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .Include(c => c.ServiceComboItems)
                    .ThenInclude(ci => ci.DepartmentService)
                .FirstOrDefaultAsync(c => c.ComboId == request.ComboId.Value && c.IsActive, ct);
            if (selectedCombo == null)
            {
                return ApiResponse<AppointmentDto>.Fail("Gói Combo dịch vụ không tồn tại hoặc đã ngừng áp dụng.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ComboCode))
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .Include(c => c.ServiceComboItems)
                    .ThenInclude(ci => ci.DepartmentService)
                .FirstOrDefaultAsync(c => c.ComboCode == request.ComboCode && c.IsActive, ct);
            if (selectedCombo == null)
            {
                return ApiResponse<AppointmentDto>.Fail("Gói Combo dịch vụ không tồn tại hoặc đã ngừng áp dụng.");
            }
        }

        if (selectedCombo != null && selectedCombo.ServiceComboItems.Any())
        {
            var firstItem = selectedCombo.ServiceComboItems.First();
            request.DepartmentId = firstItem.DepartmentService.DepartmentId;
            request.RequestedServiceId = firstItem.DepartmentServiceId;
        }

        var department = await _dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.Status == "Active", ct);

        if (department == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Chuyên khoa không tồn tại hoặc đã ngừng hoạt động.");
        }

        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.RequestedServiceId &&
                                      s.DepartmentId == request.DepartmentId &&
                                      s.Status == "Active", ct);

        if (service == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa này.");
        }

        var requestedStart = ClinicTimeHelper.ToClinicTime(request.ScheduledStart);
        var clinicNow = ClinicTimeHelper.Now;

        if (requestedStart <= clinicNow)
        {
            return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");
        }

        if (request.RequestedDentistUserId.HasValue)
        {
            var isDentistInDept = await _dbContext.DentistProfiles
                .AsNoTracking()
                .AnyAsync(d => d.UserId == request.RequestedDentistUserId.Value &&
                               d.DepartmentId == request.DepartmentId, ct);

            if (!isDentistInDept)
            {
                return ApiResponse<AppointmentDto>.Fail("Bác sĩ được yêu cầu không thuộc chuyên khoa đã chọn.");
            }
        }

        var duration = service.DurationMinutes > 0 ? service.DurationMinutes : 30;
        var scheduledEnd = requestedStart.AddMinutes(duration);
        var serviceDate = DateOnly.FromDateTime(requestedStart);

        var existingSameDayItems = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment)
            .Where(s => s.Appointment.PatientUserId == patientUserId &&
                        s.ServiceDate == serviceDate &&
                        (s.Status == AppointmentStatus.PendingReceptionReview ||
                         s.Status == AppointmentStatus.PendingDepartmentReview ||
                         s.Status == AppointmentStatus.Confirmed ||
                         s.Status == AppointmentStatus.InService))
            .ToListAsync(ct);

        if (existingSameDayItems.Any(s => s.DepartmentServiceId == request.RequestedServiceId && s.ScheduledStart == requestedStart))
        {
            return ApiResponse<AppointmentDto>.Fail("Bạn đã gửi một yêu cầu đặt lịch khám cho dịch vụ và khung giờ này rồi.");
        }

        var distinctApptCount = existingSameDayItems.Select(s => s.AppointmentId).Distinct().Count();
        if (distinctApptCount >= 2)
        {
            return ApiResponse<AppointmentDto>.Fail(
                $"Theo quy định phòng khám, mỗi bệnh nhân chỉ được đặt tối đa 2 lịch khám trong cùng một ngày. Bạn đã có {distinctApptCount} lịch hẹn vào ngày {requestedStart:dd/MM/yyyy}.");
        }

        var isNewAppointmentMorning = requestedStart.TimeOfDay < new TimeSpan(12, 0, 0);
        foreach (var existing in existingSameDayItems)
        {
            if (requestedStart < existing.ScheduledEnd && scheduledEnd > existing.ScheduledStart)
            {
                return ApiResponse<AppointmentDto>.Fail(
                    $"Bạn đã có lịch khám #APT-{existing.AppointmentId:D6} từ {existing.ScheduledStart:HH:mm} đến {existing.ScheduledEnd:HH:mm} cùng ngày.");
            }

            var isExistingMorning = existing.ScheduledStart.TimeOfDay < new TimeSpan(12, 0, 0);
            if (isNewAppointmentMorning == isExistingMorning)
            {
                return ApiResponse<AppointmentDto>.Fail("Phòng khám quy định chỉ được đặt tối đa 2 lịch/ngày nếu ở 2 buổi khác nhau (1 buổi sáng và 1 buổi chiều).");
            }
        }

        var now = DateTime.UtcNow;

        var appointment = new Appointment
        {
            PatientUserId = patientUserId,
            ComboId = selectedCombo?.ComboId,
            ReasonForVisit = string.IsNullOrWhiteSpace(request.ReasonForVisit) ? "Khám bệnh" : request.ReasonForVisit.Trim(),
            BookingSource = BookingSource.Patient,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedByUserId = patientUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(ct);

        var primaryComboItem = selectedCombo?.ServiceComboItems.FirstOrDefault(ci => ci.DepartmentServiceId == service.DepartmentServiceId)
                               ?? selectedCombo?.ServiceComboItems.FirstOrDefault();

        var apptServiceItem = new AppointmentServiceEntity
        {
            AppointmentId = appointment.AppointmentId,
            DepartmentId = (int)request.DepartmentId,
            DepartmentServiceId = service.DepartmentServiceId,
            ComboId = selectedCombo?.ComboId,
            ComboItemId = primaryComboItem?.ComboItemId,
            RequestedDentistUserId = request.RequestedDentistUserId,
            ServiceDate = serviceDate,
            ScheduledStart = requestedStart,
            ScheduledEnd = scheduledEnd,
            UnitPrice = primaryComboItem != null ? primaryComboItem.AllocatedUnitPrice : service.Price,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.AppointmentServices.Add(apptServiceItem);
        AddStatusAuditLog(appointment.AppointmentId, null, AppointmentStatus.PendingReceptionReview, patientUserId, "Bệnh nhân gửi yêu cầu đặt lịch khám.", now);

        await _dbContext.SaveChangesAsync(ct);

        var code = $"APT-{appointment.AppointmentId:D6}";
        await _notificationService.CreateNotificationAsync(
            patientUserId,
            appointment.AppointmentId,
            "AppointmentSubmitted",
            "Yêu cầu đặt lịch đã được gửi",
            $"Yêu cầu đặt lịch mã {code} đã được gửi thành công. Phòng khám sẽ xem xét và phản hồi sớm nhất.",
            ct);

        var dto = await MapToDtoAsync(appointment.AppointmentId, ct);
        if (_queueHubContext != null && dto != null)
        {
            _ = _queueHubContext.Clients.All.ReceiveAppointmentCreated(dto);
        }

        return ApiResponse<AppointmentDto>.Ok(dto!, "Yêu cầu đặt lịch khám đã được gửi thành công.");
    }

    public async Task<ApiResponse<List<AdminUserDto>>> GetPatientsAsync(string? search = null, CancellationToken ct = default)
    {
        var query = _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.PatientProfile)
            .Where(u => u.Role == UserRole.Patient)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(q)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(q)) ||
                (u.PatientProfile != null && u.PatientProfile.FullName.ToLower().Contains(q)));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserDto
            {
                UserId = u.UserId,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                Role = u.Role,
                Status = u.Status,
                FullName = u.PatientProfile != null ? u.PatientProfile.FullName : (u.Email ?? string.Empty),
                Code = $"BN-{u.UserId:D4}",
                NationalId = u.PatientProfile != null ? u.PatientProfile.NationalId : null,
                DateOfBirth = u.PatientProfile != null ? u.PatientProfile.DateOfBirth : null,
                Gender = u.PatientProfile != null ? u.PatientProfile.Gender : null,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<AdminUserDto>>.Ok(users);
    }

    public async Task<ApiResponse<RegisterWalkInPatientResponse>> RegisterWalkInPatientAsync(
        long receptionistUserId,
        RegisterWalkInPatientRequest request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Vui lòng nhập đầy đủ họ tên và số điện thoại bệnh nhân.");
        }

        var email = request.Email?.Trim().ToLowerInvariant();
        var phone = request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(email) &&
            await _dbContext.UserAccounts.AnyAsync(u => u.Email != null && u.Email.ToLower() == email, ct))
        {
            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Email này đã tồn tại trong hệ thống.");
        }

        if (await _dbContext.UserAccounts.AnyAsync(u => u.PhoneNumber == phone, ct))
        {
            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Số điện thoại này đã được đăng ký với một hồ sơ khác.");
        }

        var now = DateTime.UtcNow;
        var syntheticEmail = string.IsNullOrWhiteSpace(email)
            ? $"walkin_{Regex.Replace(phone, @"\D", "")}_{Guid.NewGuid().ToString("N")[..4]}@dental.local"
            : email;

        var normalizedGender = (request.Gender?.Trim().ToLowerInvariant()) switch
        {
            "nam" or "male" => "Male",
            "nữ" or "nu" or "female" => "Female",
            _ => "Other"
        };

        var userAccount = new UserAccount
        {
            Email = syntheticEmail,
            PhoneNumber = phone,
            PasswordHash = new PasswordHasherService().HashPassword(Guid.NewGuid().ToString("N")),
            Role = UserRole.Patient,
            Status = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.UserAccounts.Add(userAccount);
        await _dbContext.SaveChangesAsync(ct);

        var patientProfile = new PatientProfile
        {
            UserId = userAccount.UserId,
            FullName = request.FullName.Trim(),
            Gender = normalizedGender,
            DateOfBirth = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.PatientProfiles.Add(patientProfile);
        await _dbContext.SaveChangesAsync(ct);

        var patientCode = $"BN-{userAccount.UserId:D4}";
        return ApiResponse<RegisterWalkInPatientResponse>.Ok(new RegisterWalkInPatientResponse
        {
            PatientUserId = userAccount.UserId,
            PatientCode = patientCode,
            FullName = patientProfile.FullName,
            Email = userAccount.Email,
            PhoneNumber = userAccount.PhoneNumber ?? string.Empty,
            Status = userAccount.Status,
            Message = "Tiếp nhận hồ sơ bệnh nhân thành công."
        }, "Tiếp nhận hồ sơ bệnh nhân thành công.");
    }

    public async Task<ApiResponse<AppointmentDto>> CreateReceptionistAppointmentAsync(
        long receptionistUserId,
        CreateReceptionistAppointmentRequest request,
        CancellationToken ct = default)
    {
        if (request == null || request.PatientUserId <= 0)
        {
            return ApiResponse<AppointmentDto>.Fail("Vui lòng chọn bệnh nhân hợp lệ.");
        }

        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null || patient.User.Status == AccountStatus.Locked || patient.User.Status == AccountStatus.Inactive)
        {
            return ApiResponse<AppointmentDto>.Fail("Hồ sơ bệnh nhân không hợp lệ hoặc đã bị khóa.");
        }

        ServiceCombo? selectedCombo = null;
        if (request.ComboId.HasValue && request.ComboId.Value > 0)
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .Include(c => c.ServiceComboItems)
                    .ThenInclude(ci => ci.DepartmentService)
                .FirstOrDefaultAsync(c => c.ComboId == request.ComboId.Value && c.IsActive, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.ComboCode))
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .Include(c => c.ServiceComboItems)
                    .ThenInclude(ci => ci.DepartmentService)
                .FirstOrDefaultAsync(c => c.ComboCode == request.ComboCode && c.IsActive, ct);
        }

        if (selectedCombo != null && selectedCombo.ServiceComboItems.Any())
        {
            var firstItem = selectedCombo.ServiceComboItems.First();
            request.DepartmentId = firstItem.DepartmentService.DepartmentId;
            request.RequestedServiceId = firstItem.DepartmentServiceId;
        }

        var department = await _dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.Status == "Active", ct);
        if (department == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Chuyên khoa không tồn tại hoặc đã ngừng hoạt động.");
        }

        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.RequestedServiceId &&
                                      s.DepartmentId == request.DepartmentId &&
                                      s.Status == "Active", ct);
        if (service == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa này.");
        }

        var requestedStart = ClinicTimeHelper.ToClinicTime(request.ScheduledStart);
        var duration = service.DurationMinutes > 0 ? service.DurationMinutes : 30;
        var scheduledEnd = requestedStart.AddMinutes(duration);
        var serviceDate = DateOnly.FromDateTime(requestedStart);

        bool hasAllResources = request.AssignedDentistUserId.HasValue && request.AssignedDentistUserId.Value > 0 &&
                               request.RoomId.HasValue && request.RoomId.Value > 0 &&
                               request.DentalChairId.HasValue && request.DentalChairId.Value > 0;

        bool shouldConfirm = request.AutoConfirm || hasAllResources;
        if (request.AutoConfirm && !hasAllResources)
        {
            return ApiResponse<AppointmentDto>.Fail("Vui lòng phân bổ đầy đủ Bác sĩ, Phòng khám và Ghế nha khoa để xác nhận lịch hẹn tại quầy.");
        }

        int? queueNumber = null;
        if (shouldConfirm)
        {
            var maxQueue = await _dbContext.AppointmentServices
                .AsNoTracking()
                .Where(s => s.DepartmentId == request.DepartmentId && s.ServiceDate == serviceDate && s.QueueNumber.HasValue)
                .Select(s => s.QueueNumber!.Value)
                .DefaultIfEmpty(0)
                .MaxAsync(ct);
            queueNumber = maxQueue + 1;
        }

        var now = DateTime.UtcNow;
        var finalStatus = (request.AutoCheckIn && shouldConfirm)
            ? AppointmentStatus.CheckedIn
            : (shouldConfirm ? AppointmentStatus.Confirmed : AppointmentStatus.PendingReceptionReview);

        var appointment = new Appointment
        {
            PatientUserId = request.PatientUserId,
            ComboId = selectedCombo?.ComboId,
            ReasonForVisit = string.IsNullOrWhiteSpace(request.ReasonForVisit) ? "Khám bệnh tại quầy" : request.ReasonForVisit.Trim(),
            BookingSource = BookingSource.Receptionist,
            Status = finalStatus,
            ConfirmedAt = shouldConfirm ? now : null,
            CheckedInAt = (request.AutoCheckIn && shouldConfirm) ? now : null,
            CreatedByUserId = receptionistUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(ct);

        var primaryComboItem = selectedCombo?.ServiceComboItems.FirstOrDefault(ci => ci.DepartmentServiceId == service.DepartmentServiceId)
                               ?? selectedCombo?.ServiceComboItems.FirstOrDefault();

        var apptItem = new AppointmentServiceEntity
        {
            AppointmentId = appointment.AppointmentId,
            DepartmentId = (int)request.DepartmentId,
            DepartmentServiceId = service.DepartmentServiceId,
            ComboId = selectedCombo?.ComboId,
            ComboItemId = primaryComboItem?.ComboItemId,
            RequestedDentistUserId = request.RequestedDentistUserId,
            AssignedDentistUserId = shouldConfirm ? request.AssignedDentistUserId : null,
            RoomId = shouldConfirm && request.RoomId.HasValue ? (int)request.RoomId.Value : null,
            DentalChairId = shouldConfirm && request.DentalChairId.HasValue ? (int)request.DentalChairId.Value : null,
            ServiceDate = serviceDate,
            ScheduledStart = requestedStart,
            ScheduledEnd = scheduledEnd,
            QueueNumber = queueNumber,
            UnitPrice = primaryComboItem != null ? primaryComboItem.AllocatedUnitPrice : service.Price,
            Status = finalStatus,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.AppointmentServices.Add(apptItem);

        AddStatusAuditLog(
            appointment.AppointmentId,
            null,
            finalStatus,
            receptionistUserId,
            shouldConfirm
                ? $"Lễ tân tiếp nhận và xác nhận lịch hẹn tại quầy (STT: {queueNumber})."
                : "Lễ tân tạo yêu cầu đặt lịch hẹn tại quầy.",
            now);

        await _dbContext.SaveChangesAsync(ct);

        var dto = await MapToDtoAsync(appointment.AppointmentId, ct);
        return ApiResponse<AppointmentDto>.Ok(dto!, "Tạo lịch hẹn tại quầy thành công.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetPatientAppointmentsAsync(
        long patientUserId,
        string? statusFilter = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.PatientUserId == patientUserId);

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var filter = statusFilter.Trim().ToLowerInvariant();
            if (filter == "upcoming")
            {
                query = query.Where(a => a.Status == AppointmentStatus.Confirmed ||
                                         a.Status == AppointmentStatus.CheckedIn ||
                                         a.Status == AppointmentStatus.Waiting ||
                                         a.Status == AppointmentStatus.Called ||
                                         a.Status == AppointmentStatus.InService);
            }
            else if (filter == "pending")
            {
                query = query.Where(a => a.Status == AppointmentStatus.PendingReceptionReview ||
                                         a.Status == AppointmentStatus.PendingDepartmentReview ||
                                         a.Status == AppointmentStatus.AwaitingPatientResponse);
            }
            else if (filter == "completed")
            {
                query = query.Where(a => a.Status == AppointmentStatus.Completed);
            }
            else if (filter == "cancelled")
            {
                query = query.Where(a => a.Status == AppointmentStatus.Cancelled ||
                                         a.Status == AppointmentStatus.Rejected ||
                                         a.Status == AppointmentStatus.Withdrawn ||
                                         a.Status == AppointmentStatus.Expired ||
                                         a.Status == AppointmentStatus.NoShow);
            }
        }

        var list = await MapToDtoListAsync(query.OrderByDescending(a => a.CreatedAt), ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<AppointmentDto>> GetAppointmentDetailAsync(
        long appointmentId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        if (currentRole == UserRole.Patient && appointment.PatientUserId != currentUserId)
        {
            return ApiResponse<AppointmentDto>.Fail("Bạn không có quyền truy cập lịch hẹn này.");
        }

        if (currentRole == UserRole.Dentist && !appointment.AppointmentServices.Any(s => s.AssignedDentistUserId == currentUserId))
        {
            return ApiResponse<AppointmentDto>.Fail("Lịch hẹn này không được phân công cho bạn.");
        }

        var dto = await MapToDtoAsync(appointmentId, ct);
        return ApiResponse<AppointmentDto>.Ok(dto!);
    }

    public async Task<ApiResponse> PatientWithdrawAsync(
        long appointmentId,
        long patientUserId,
        string? reason,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn hoặc bạn không có quyền rút yêu cầu.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Withdrawn;
        appointment.CancelledAt = now;
        appointment.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Bệnh nhân rút lại yêu cầu đặt lịch." : reason.Trim();
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.Withdrawn;
            item.QueueNumber = null;
            item.PendingActionType = null;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.Withdrawn, patientUserId, appointment.CancellationReason, now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã rút yêu cầu đặt lịch khám thành công.");
    }

    public async Task<ApiResponse> PatientCancelAsync(
        long appointmentId,
        long patientUserId,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse.Fail("Vui lòng cung cấp lý do hủy lịch.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn hoặc bạn không có quyền hủy lịch hẹn này.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        var localNow = ClinicTimeHelper.Now;
        bool isLate = primaryItem != null && (primaryItem.ScheduledStart - localNow).TotalHours < 12;

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = now;
        appointment.CancellationReason = $"{(isLate ? "[Hủy sát giờ <12h] " : "")}{reason.Trim()}";
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.Cancelled;
            item.QueueNumber = null;
            item.PendingActionType = null;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.Cancelled, patientUserId, $"Bệnh nhân hủy lịch: {reason.Trim()}", now);

        var linkedFollowUp = await _dbContext.FollowUpSchedules
            .FirstOrDefaultAsync(f => f.ResultingAppointmentId == appointment.AppointmentId, ct);
        if (linkedFollowUp != null && linkedFollowUp.Status != "Completed")
        {
            linkedFollowUp.Status = "Cancelled";
            linkedFollowUp.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Hủy lịch hẹn thành công.");
    }

    public async Task<ApiResponse<AppointmentDto>> PatientRescheduleAsync(
        long appointmentId,
        long patientUserId,
        PatientRescheduleRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<AppointmentDto>.Fail("Vui lòng nhập lý do dời lịch hẹn.");
        }

        var newStart = ClinicTimeHelper.ToClinicTime(request.NewScheduledStart);
        if (newStart <= ClinicTimeHelper.Now)
        {
            return ApiResponse<AppointmentDto>.Fail("Thời gian khám mới không được nằm trong quá khứ.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy lịch hẹn hoặc bạn không có quyền dời lịch hẹn này.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        var duration = primaryItem?.DepartmentService?.DurationMinutes ?? 30;
        var scheduledEnd = newStart.AddMinutes(duration > 0 ? duration : 30);
        var now = DateTime.UtcNow;
        var oldCode = $"APT-{appointment.AppointmentId:D6}";

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = now;
        appointment.CancellationReason = $"[Dời lịch] Hủy để dời sang lịch khám mới. Lý do: {request.Reason.Trim()}";
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.Cancelled;
            item.RoomId = null;
            item.DentalChairId = null;
            item.AssignedDentistUserId = null;
            item.QueueNumber = null;
            item.UpdatedAt = now;
        }

        var newAppointment = new Appointment
        {
            PatientUserId = patientUserId,
            ComboId = appointment.ComboId,
            ReasonForVisit = $"{appointment.ReasonForVisit} (Dời từ lịch {oldCode})",
            BookingSource = BookingSource.Patient,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedByUserId = patientUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Appointments.Add(newAppointment);
        await _dbContext.SaveChangesAsync(ct);

        var newCode = $"APT-{newAppointment.AppointmentId:D6}";
        AddStatusAuditLog(appointment.AppointmentId, oldStatus, AppointmentStatus.Cancelled, patientUserId, $"Bệnh nhân hủy hẹn để dời sang lịch mới: {newCode}.", now);

        var newItem = new AppointmentServiceEntity
        {
            AppointmentId = newAppointment.AppointmentId,
            DepartmentId = primaryItem?.DepartmentId ?? 1,
            DepartmentServiceId = primaryItem?.DepartmentServiceId ?? 1,
            ComboId = primaryItem?.ComboId,
            ComboItemId = primaryItem?.ComboItemId,
            RequestedDentistUserId = request.NewRequestedDentistUserId ?? primaryItem?.RequestedDentistUserId,
            ServiceDate = DateOnly.FromDateTime(newStart),
            ScheduledStart = newStart,
            ScheduledEnd = scheduledEnd,
            UnitPrice = primaryItem?.UnitPrice ?? 0m,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.AppointmentServices.Add(newItem);

        AddStatusAuditLog(newAppointment.AppointmentId, null, AppointmentStatus.PendingReceptionReview, patientUserId, $"Đặt lịch mới chuyển từ lịch đã hủy {oldCode}.", now);

        await _dbContext.SaveChangesAsync(ct);

        var dto = await MapToDtoAsync(newAppointment.AppointmentId, ct);
        return ApiResponse<AppointmentDto>.Ok(dto!, $"Đã dời sang lịch hẹn mới ({newCode}) thành công!");
    }

    public async Task<ApiResponse<AppointmentChangeProposalDto>> GetAppointmentProposalAsync(
        long appointmentId,
        long patientUserId,
        CancellationToken ct = default)
    {
        var item = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment)
            .Include(s => s.DepartmentService).ThenInclude(ds => ds.Department)
            .Include(s => s.PendingDentist).ThenInclude(d => d!.StaffProfile).ThenInclude(sp => sp.User)
            .Where(s => s.AppointmentId == appointmentId && s.Appointment.PatientUserId == patientUserId && s.PendingActionType != null)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (item == null)
        {
            return ApiResponse<AppointmentChangeProposalDto>.Fail("Không có đề xuất thay đổi nào đang chờ phản hồi.");
        }

        return ApiResponse<AppointmentChangeProposalDto>.Ok(new AppointmentChangeProposalDto
        {
            ProposalId = item.AppointmentServiceId,
            AppointmentId = item.AppointmentId,
            ProposedByUserId = item.PendingDentistUserId ?? 0,
            ProposedByName = "Phòng khám",
            ProposedDepartmentId = item.DepartmentId,
            ProposedDepartmentName = item.DepartmentService?.Department?.Name,
            ProposedDentistUserId = item.PendingDentistUserId,
            ProposedDentistName = item.PendingDentist?.StaffProfile?.FullName,
            ProposedStart = item.PendingScheduledStart,
            ProposedEnd = item.PendingScheduledEnd,
            Reason = item.Notes ?? "Đề xuất thay đổi lịch khám",
            Status = ProposalStatus.Pending,
            CreatedAt = item.UpdatedAt ?? item.CreatedAt
        });
    }

    public async Task<ApiResponse> PatientRespondProposalAsync(
        long appointmentId,
        long patientUserId,
        bool accept,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId && a.PatientUserId == patientUserId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        var item = appointment.AppointmentServices.FirstOrDefault(s => s.PendingActionType != null)
                   ?? appointment.AppointmentServices.FirstOrDefault();

        if (item == null)
        {
            return ApiResponse.Fail("Không tìm thấy đề xuất thay đổi nào đang chờ.");
        }

        var now = DateTime.UtcNow;
        if (accept)
        {
            if (item.PendingDentistUserId.HasValue)
                item.RequestedDentistUserId = item.PendingDentistUserId.Value;
            if (item.PendingScheduledStart.HasValue)
            {
                item.ScheduledStart = item.PendingScheduledStart.Value;
                item.ServiceDate = DateOnly.FromDateTime(item.PendingScheduledStart.Value);
            }
            if (item.PendingScheduledEnd.HasValue)
                item.ScheduledEnd = item.PendingScheduledEnd.Value;
        }

        item.PendingActionType = null;
        item.PendingDentistUserId = null;
        item.PendingScheduledStart = null;
        item.PendingScheduledEnd = null;
        item.Status = AppointmentStatus.PendingReceptionReview;
        item.UpdatedAt = now;

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.PendingReceptionReview;
        appointment.UpdatedAt = now;

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.PendingReceptionReview, patientUserId, accept ? "Bệnh nhân đồng ý với đề xuất thay đổi." : "Bệnh nhân từ chối đề xuất thay đổi.", now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok(accept ? "Bạn đã chấp nhận đề xuất thay đổi lịch hẹn." : "Bạn đã từ chối đề xuất thay đổi.");
    }

    public async Task<ApiResponse<BookingEligibilityDto>> GetBookingEligibilityAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);

        var candidateAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.AppointmentServices)
            .Where(a => a.PatientUserId == patientUserId &&
                        (a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow))
            .OrderByDescending(a => a.CancelledAt ?? a.CreatedAt)
            .ToListAsync(ct);

        var strikes = new List<StrikeIncidentDto>();

        foreach (var appt in candidateAppointments)
        {
            var firstItem = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
            var apptCode = $"APT-{appt.AppointmentId:D6}";
            if (appt.Status == AppointmentStatus.Cancelled)
            {
                var incidentDate = appt.CancelledAt ?? appt.UpdatedAt ?? appt.CreatedAt;
                if (incidentDate < thirtyDaysAgo) continue;

                bool isLate = appt.CancellationReason != null && appt.CancellationReason.Contains("[Hủy sát giờ <12h]");
                if (isLate)
                {
                    strikes.Add(new StrikeIncidentDto
                    {
                        AppointmentId = appt.AppointmentId,
                        AppointmentCode = apptCode,
                        Type = "LateCancellation",
                        Description = $"Hủy lịch hẹn sát giờ (<12h): {appt.CancellationReason}",
                        IncidentDate = incidentDate
                    });
                }
            }
            else if (appt.Status == AppointmentStatus.NoShow)
            {
                var incidentDate = firstItem?.ScheduledStart ?? appt.UpdatedAt ?? appt.CreatedAt;
                if (incidentDate >= thirtyDaysAgo)
                {
                    strikes.Add(new StrikeIncidentDto
                    {
                        AppointmentId = appt.AppointmentId,
                        AppointmentCode = apptCode,
                        Type = "NoShow",
                        Description = "Không đến khám đúng hẹn (No-Show)",
                        IncidentDate = incidentDate
                    });
                }
            }
        }

        var result = new BookingEligibilityDto
        {
            StrikeCount = strikes.Count,
            MaxStrikesAllowed = 3,
            IsEligible = true,
            Strikes = strikes.OrderByDescending(s => s.IncidentDate).ToList()
        };

        if (strikes.Count >= 3)
        {
            var latestIncident = strikes.Max(s => s.IncidentDate);
            var blockedUntil = latestIncident.AddDays(30);
            if (blockedUntil > now)
            {
                result.IsEligible = false;
                result.BlockedUntil = blockedUntil;
                result.BlockReason = $"Tài khoản đã tích lũy {strikes.Count} lần vi phạm trong 30 ngày qua. Quyền tự đặt lịch trực tuyến tạm khóa đến {blockedUntil:dd/MM/yyyy HH:mm}.";
            }
        }

        return ApiResponse<BookingEligibilityDto>.Ok(result);
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetReceptionistRequestsAsync(CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.PendingReceptionReview)
            .OrderByDescending(a => a.CreatedAt);

        var list = await MapToDtoListAsync(query, ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse> ReceptionistForwardAsync(
        long appointmentId,
        long staffUserId,
        string? note = null,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.PendingDepartmentReview;
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.PendingDepartmentReview;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.PendingDepartmentReview, staffUserId, string.IsNullOrWhiteSpace(note) ? "Lễ tân tiếp nhận và chuyển tiếp xem xét." : note.Trim(), now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã chuyển tiếp yêu cầu thành công.");
    }

    public async Task<ApiResponse> ReceptionistRejectAsync(
        long appointmentId,
        long staffUserId,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse.Fail("Vui lòng nhập lý do từ chối yêu cầu đặt lịch.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Rejected;
        appointment.CancelledAt = now;
        appointment.CancellationReason = reason.Trim();
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.Rejected;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.Rejected, staffUserId, $"Lễ tân từ chối yêu cầu: {reason.Trim()}", now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã từ chối yêu cầu đặt lịch.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetReceptionistTodayConfirmedAsync(
        string? searchQuery = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? shift = null,
        long? departmentId = null,
        long? dentistId = null,
        string? status = null,
        CancellationToken ct = default)
    {
        DateTime startRange = fromDate?.Date ?? ClinicTimeHelper.Today.Date;
        DateTime endRange = toDate.HasValue ? toDate.Value.Date.AddDays(1) : startRange.AddDays(1);

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentServices.Any(s => s.ScheduledStart >= startRange && s.ScheduledStart < endRange));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusStr = status.Trim();
            query = query.Where(a => a.Status == statusStr);
        }
        else
        {
            query = query.Where(a =>
                a.Status == AppointmentStatus.Confirmed ||
                a.Status == AppointmentStatus.CheckedIn ||
                a.Status == AppointmentStatus.Waiting ||
                a.Status == AppointmentStatus.InService ||
                a.Status == AppointmentStatus.Completed ||
                a.Status == AppointmentStatus.NoShow);
        }

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            query = query.Where(a => a.AppointmentServices.Any(s => s.DepartmentId == departmentId.Value));
        }

        if (dentistId.HasValue && dentistId.Value > 0)
        {
            query = query.Where(a => a.AppointmentServices.Any(s =>
                s.AssignedDentistUserId == dentistId.Value ||
                (s.AssignedDentistUserId == null && s.RequestedDentistUserId == dentistId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim().ToLower();
            query = query.Where(a =>
                a.AppointmentId.ToString().Contains(q) ||
                a.Patient.FullName.ToLower().Contains(q) ||
                (a.Patient.User.PhoneNumber != null && a.Patient.User.PhoneNumber.Contains(q)));
        }

        var list = await MapToDtoListAsync(query.OrderBy(a => a.CreatedAt), ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse> ReceptionistCheckInAsync(
        long appointmentId,
        long staffUserId,
        string? note = null,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return ApiResponse.Fail($"Chỉ có thể check-in cho lịch hẹn ở trạng thái Confirmed (Hiện tại: {appointment.Status}).");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        int queueSeq = primaryItem?.QueueNumber ?? 1;
        if (primaryItem != null && !primaryItem.QueueNumber.HasValue)
        {
            var maxQ = await _dbContext.AppointmentServices
                .AsNoTracking()
                .Where(s => s.DepartmentId == primaryItem.DepartmentId && s.ServiceDate == primaryItem.ServiceDate && s.QueueNumber.HasValue)
                .Select(s => s.QueueNumber!.Value)
                .DefaultIfEmpty(0)
                .MaxAsync(ct);
            queueSeq = maxQ + 1;
            primaryItem.QueueNumber = queueSeq;
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.CheckedIn;
        appointment.CheckedInAt = now;
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.CheckedIn;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(
            appointmentId,
            oldStatus,
            AppointmentStatus.CheckedIn,
            staffUserId,
            string.IsNullOrWhiteSpace(note)
                ? $"Bệnh nhân đã đến và hoàn tất thủ tục Check-in. STT: {queueSeq}."
                : $"{note.Trim()} (STT: {queueSeq})",
            now);

        await _dbContext.SaveChangesAsync(ct);

        var queueDto = await MapToDtoAsync(appointment.AppointmentId, ct);
        if (_queueHubContext != null && queueDto != null)
        {
            _ = _queueHubContext.Clients.All.ReceivePatientCheckedIn(queueDto);
        }
        return ApiResponse.Ok($"Check-in bệnh nhân thành công. Số thứ tự: {queueSeq}.");
    }

    public async Task<ApiResponse> ReceptionistMarkNoShowAsync(
        long appointmentId,
        long staffUserId,
        string? note = null,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.NoShow;
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.NoShow;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.NoShow, staffUserId, string.IsNullOrWhiteSpace(note) ? "Lễ tân đánh dấu bệnh nhân không đến khám (No-Show)." : note.Trim(), now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã ghi nhận bệnh nhân vắng mặt (No-Show) thành công.");
    }

    public async Task<ApiResponse> StaffCancelAppointmentAsync(
        long appointmentId,
        long staffUserId,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse.Fail("Vui lòng cung cấp lý do hủy lịch hẹn.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = now;
        appointment.CancellationReason = $"[Lễ tân hủy] {reason.Trim()}";
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.Cancelled;
            item.QueueNumber = null;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.Cancelled, staffUserId, $"Lễ tân/Nhân viên hủy lịch: {reason.Trim()}", now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã hủy lịch hẹn thành công (không tính điểm phạt cho bệnh nhân).");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDepartmentRequestsAsync(
        long managerUserId,
        CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.PendingReceptionReview || a.Status == AppointmentStatus.PendingDepartmentReview)
            .OrderByDescending(a => a.CreatedAt);

        var list = await MapToDtoListAsync(query, ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<DentistOptionDto>>> GetAvailableDentistsForAppointmentAsync(
        long appointmentId,
        long managerUserId,
        CancellationToken ct = default)
    {
        var apptItem = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(s => s.AppointmentId == appointmentId)
            .OrderBy(s => s.ScheduledStart)
            .FirstOrDefaultAsync(ct);

        if (apptItem == null)
        {
            return ApiResponse<List<DentistOptionDto>>.Fail("Không tìm thấy thông tin lịch hẹn hoặc chưa có thời gian khám.");
        }

        var deptId = apptItem.DepartmentId;
        var start = apptItem.ScheduledStart;
        var end = apptItem.ScheduledEnd;
        var targetDateOnly = apptItem.ServiceDate;
        var startTimeOnly = TimeOnly.FromDateTime(start);
        var endTimeOnly = TimeOnly.FromDateTime(end);

        var dentistsInDept = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(sp => sp.User)
            .Where(d => d.DepartmentId == deptId && d.StaffProfile.EmploymentStatus == "Active")
            .ToListAsync(ct);

        var candidateIds = dentistsInDept.Select(d => d.UserId).ToList();

        var scheduledDentistIds = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => candidateIds.Contains(ws.StaffUserId) &&
                         ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate == targetDateOnly &&
                         ws.Status == "Scheduled" &&
                         ws.StartTime <= startTimeOnly &&
                         ws.EndTime >= endTimeOnly)
            .Select(ws => ws.StaffUserId)
            .ToListAsync(ct);

        var unavailableDentistIds = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => candidateIds.Contains(l.StaffUserId) &&
                        l.StaffRole == UserRole.Dentist &&
                        l.Status == "Approved" &&
                        l.StartDate <= targetDateOnly &&
                        l.EndDate >= targetDateOnly)
            .Select(l => l.StaffUserId)
            .Distinct()
            .ToListAsync(ct);

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var conflictedDentistIds = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.AppointmentId != appointmentId &&
                        a.AssignedDentistUserId != null &&
                        candidateIds.Contains(a.AssignedDentistUserId.Value) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < end &&
                        a.ScheduledEnd > start)
            .Select(a => a.AssignedDentistUserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var resultDentists = dentistsInDept
            .Select(d => new DentistOptionDto
            {
                UserId = d.UserId,
                FullName = d.StaffProfile.FullName,
                EmployeeCode = $"BS-{d.UserId:D4}",
                Specialization = d.Qualification,
                Qualification = d.Qualification,
                YearsOfExperience = d.YearsOfExperience,
                Biography = d.Biography,
                IsAvailable = scheduledDentistIds.Contains(d.UserId) &&
                              !unavailableDentistIds.Contains(d.UserId) &&
                              !conflictedDentistIds.Contains(d.UserId)
            })
            .OrderByDescending(d => d.IsAvailable)
            .ThenBy(d => d.FullName)
            .ToList();

        return ApiResponse<List<DentistOptionDto>>.Ok(resultDentists);
    }

    public async Task<ApiResponse<AvailableResourcesDto>> GetAvailableResourcesAsync(
        long departmentId,
        DateTime start,
        DateTime end,
        CancellationToken ct = default)
    {
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var conflictingItems = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < end &&
                        a.ScheduledEnd > start)
            .ToListAsync(ct);

        var occupiedRoomIds = conflictingItems
            .Where(a => a.RoomId.HasValue)
            .Select(a => a.RoomId!.Value)
            .ToHashSet();

        var occupiedChairIds = conflictingItems
            .Where(a => a.DentalChairId.HasValue)
            .Select(a => a.DentalChairId!.Value)
            .ToHashSet();

        var allRooms = await _dbContext.Rooms
            .AsNoTracking()
            .Include(r => r.DentalChairs)
            .Where(r => r.Status == "Active" && (departmentId <= 0 || r.DepartmentId == departmentId))
            .OrderBy(r => r.RoomCode)
            .ToListAsync(ct);

        var availableRooms = allRooms
            .Where(r =>
            {
                bool isExclusive = string.Equals(r.RoomType, "Exclusive", StringComparison.OrdinalIgnoreCase);
                if (isExclusive)
                {
                    return !occupiedRoomIds.Contains(r.RoomId);
                }
                var activeChairs = r.DentalChairs.Where(c => c.Status == "Active").ToList();
                if (activeChairs.Count == 0) return !occupiedRoomIds.Contains(r.RoomId);
                return activeChairs.Any(c => !occupiedChairIds.Contains(c.DentalChairId));
            })
            .Select(r => new RoomOptionDto
            {
                RoomId = r.RoomId,
                RoomCode = r.RoomCode,
                RoomName = r.RoomName,
                DepartmentId = r.DepartmentId,
                Status = r.Status,
                IsExclusive = string.Equals(r.RoomType, "Exclusive", StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        var availableRoomIds = availableRooms.Select(r => (int)r.RoomId).ToHashSet();

        var chairs = await _dbContext.DentalChairs
            .AsNoTracking()
            .Where(c => c.Status == "Active" && availableRoomIds.Contains(c.RoomId) && !occupiedChairIds.Contains(c.DentalChairId))
            .OrderBy(c => c.ChairCode)
            .Select(c => new ChairOptionDto
            {
                DentalChairId = c.DentalChairId,
                RoomId = c.RoomId,
                ChairCode = c.ChairCode,
                Status = c.Status
            })
            .ToListAsync(ct);

        var dentistsInDept = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(sp => sp.User)
            .Where(d => d.DepartmentId == departmentId && d.StaffProfile.EmploymentStatus == "Active")
            .Select(d => new DentistOptionDto
            {
                UserId = d.UserId,
                FullName = d.StaffProfile.FullName,
                Qualification = d.Qualification,
                YearsOfExperience = d.YearsOfExperience,
                Biography = d.Biography
            })
            .ToListAsync(ct);

        return ApiResponse<AvailableResourcesDto>.Ok(new AvailableResourcesDto
        {
            Rooms = availableRooms,
            Chairs = chairs,
            Dentists = dentistsInDept
        });
    }

    public async Task<ApiResponse<AppointmentDto>> ManagerConfirmAsync(
        long appointmentId,
        long managerUserId,
        ManagerConfirmRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            var existingDto = await MapToDtoAsync(appointmentId, ct);
            return ApiResponse<AppointmentDto>.Ok(existingDto!, "Lịch hẹn đã được xác nhận trước đó.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Lịch hẹn chưa có dịch vụ liên kết.");
        }

        var finalStart = request.ScheduledStart ?? primaryItem.ScheduledStart;
        var finalEnd = request.ScheduledEnd ?? primaryItem.ScheduledEnd;
        var targetDateOnly = DateOnly.FromDateTime(finalStart);

        int queueSeq = primaryItem.QueueNumber ?? 0;
        if (queueSeq <= 0)
        {
            var maxQ = await _dbContext.AppointmentServices
                .AsNoTracking()
                .Where(s => s.DepartmentId == primaryItem.DepartmentId && s.ServiceDate == targetDateOnly && s.QueueNumber.HasValue)
                .Select(s => s.QueueNumber!.Value)
                .DefaultIfEmpty(0)
                .MaxAsync(ct);
            queueSeq = maxQ + 1;
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Confirmed;
        appointment.ConfirmedAt = now;
        appointment.UpdatedAt = now;

        primaryItem.AssignedDentistUserId = request.AssignedDentistUserId;
        primaryItem.RoomId = (int)request.RoomId;
        primaryItem.DentalChairId = request.DentalChairId.HasValue ? (int)request.DentalChairId.Value : null;
        primaryItem.ServiceDate = targetDateOnly;
        primaryItem.ScheduledStart = finalStart;
        primaryItem.ScheduledEnd = finalEnd;
        primaryItem.QueueNumber = queueSeq;
        primaryItem.Status = AppointmentStatus.Confirmed;
        primaryItem.UpdatedAt = now;

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.Confirmed, managerUserId, $"Xác nhận lịch hẹn và phân bổ bác sĩ/phòng/ghế (STT: {queueSeq}).", now);

        await _dbContext.SaveChangesAsync(ct);

        var dto = await MapToDtoAsync(appointmentId, ct);
        return ApiResponse<AppointmentDto>.Ok(dto!, "Xác nhận lịch hẹn thành công.");
    }

    public async Task<ApiResponse> ManagerProposeChangeAsync(
        long appointmentId,
        long managerUserId,
        ManagerProposeChangeRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse.Fail("Vui lòng nhập lý do đề xuất thay đổi lịch hẹn.");
        }

        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse.Fail("Lịch hẹn chưa có dịch vụ.");
        }

        var now = DateTime.UtcNow;
        primaryItem.PendingActionType = "Reschedule";
        primaryItem.PendingDentistUserId = request.ProposedDentistUserId;
        primaryItem.PendingScheduledStart = request.ProposedStart;
        primaryItem.PendingScheduledEnd = request.ProposedEnd;
        primaryItem.Notes = request.Reason.Trim();
        primaryItem.UpdatedAt = now;

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.AwaitingPatientResponse;
        appointment.UpdatedAt = now;

        AddStatusAuditLog(appointmentId, oldStatus, AppointmentStatus.AwaitingPatientResponse, managerUserId, $"Đề xuất thay đổi: {request.Reason.Trim()}", now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã gửi đề xuất thay đổi lịch hẹn đến Bệnh nhân thành công.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDentistTodayAppointmentsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(ClinicTimeHelper.Today);
        var visibleStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService,
            AppointmentStatus.Completed
        };

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentServices.Any(s =>
                (s.AssignedDentistUserId == dentistUserId || (s.AssignedDentistUserId == null && s.RequestedDentistUserId == dentistUserId)) &&
                s.ServiceDate == today &&
                visibleStatuses.Contains(s.Status)));

        var list = await MapToDtoListAsync(query.OrderBy(a => a.CreatedAt), ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDentistAppointmentsAsync(
        long dentistUserId,
        DentistAppointmentFilterQuery filter,
        CancellationToken ct = default)
    {
        if (filter == null)
        {
            return await GetDentistAppointmentsAsync(dentistUserId, ct: ct);
        }

        return await GetDentistAppointmentsAsync(
            dentistUserId,
            filter.FromDate,
            filter.ToDate,
            filter.FromTime,
            filter.ToTime,
            filter.Status,
            filter.RoomId,
            filter.DentalChairId,
            filter.SearchTerm,
            ct);
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDentistAppointmentsAsync(
        long dentistUserId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        TimeOnly? fromTime = null,
        TimeOnly? toTime = null,
        string? status = null,
        long? roomId = null,
        long? dentalChairId = null,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentServices.Any(s => s.AssignedDentistUserId == dentistUserId));

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.AppointmentServices.Any(s => s.ServiceDate >= fromDate.Value));
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.AppointmentServices.Any(s => s.ServiceDate <= toDate.Value));
        }

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(a => a.Status == status);
        }

        if (roomId.HasValue && roomId.Value > 0)
        {
            query = query.Where(a => a.AppointmentServices.Any(s => s.RoomId == roomId.Value));
        }

        if (dentalChairId.HasValue && dentalChairId.Value > 0)
        {
            query = query.Where(a => a.AppointmentServices.Any(s => s.DentalChairId == dentalChairId.Value));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(a =>
                a.AppointmentId.ToString().Contains(term) ||
                a.Patient.FullName.ToLower().Contains(term) ||
                (a.Patient.User.PhoneNumber != null && a.Patient.User.PhoneNumber.Contains(term)));
        }

        var list = await MapToDtoListAsync(query.OrderByDescending(a => a.CreatedAt), ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<RoomOptionDto>>> GetDentistRoomsAsync(long dentistUserId, CancellationToken ct = default)
    {
        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        var query = _dbContext.Rooms.AsNoTracking().Where(r => r.Status == "Active");
        if (dentist != null)
        {
            query = query.Where(r => r.DepartmentId == dentist.DepartmentId);
        }

        var rooms = await query
            .OrderBy(r => r.RoomName)
            .Select(r => new RoomOptionDto
            {
                RoomId = r.RoomId,
                RoomCode = r.RoomCode,
                RoomName = r.RoomName,
                DepartmentId = r.DepartmentId,
                Status = r.Status
            })
            .ToListAsync(ct);

        return ApiResponse<List<RoomOptionDto>>.Ok(rooms);
    }

    public async Task<ApiResponse<StartAppointmentResultDto>> DentistStartAppointmentAsync(
        long appointmentId,
        long dentistUserId,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<StartAppointmentResultDto>.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse<StartAppointmentResultDto>.Fail("Ca khám chưa có dịch vụ liên kết.");
        }

        var now = DateTime.UtcNow;
        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.InService;
        appointment.UpdatedAt = now;

        primaryItem.Status = AppointmentStatus.InService;
        primaryItem.UpdatedAt = now;

        AddStatusAuditLog(appointment.AppointmentId, oldStatus, AppointmentStatus.InService, dentistUserId, "Bác sĩ tiếp nhận bệnh nhân vào ghế điều trị.", now);

        await _dbContext.SaveChangesAsync(ct);

        var resultDto = new StartAppointmentResultDto
        {
            AppointmentId = appointment.AppointmentId,
            PatientUserId = appointment.PatientUserId,
            Status = AppointmentStatus.InService,
            DentalChairId = primaryItem.DentalChairId
        };

        var queueDto = await MapToDtoAsync(appointment.AppointmentId, ct);
        if (_queueHubContext != null && queueDto != null)
        {
            _ = _queueHubContext.Clients.All.ReceivePatientInService(queueDto);
        }
        return ApiResponse<StartAppointmentResultDto>.Ok(resultDto, "Đã tiếp nhận bệnh nhân vào ghế điều trị thành công.");
    }

    public async Task<ApiResponse> DentistCompleteAppointmentAsync(
        long appointmentId,
        long dentistUserId,
        string? notes = null,
        string? followUpReason = null,
        DateTime? recommendedFollowUpDate = null,
        string? followUpReasonCategory = null,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .Include(a => a.TreatmentSessions)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        var now = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(followUpReason) || recommendedFollowUpDate.HasValue)
        {
            var session = appointment.TreatmentSessions.FirstOrDefault();
            if (session == null && primaryItem != null)
            {
                session = new TreatmentSession
                {
                    AppointmentId = appointment.AppointmentId,
                    AppointmentServiceId = primaryItem.AppointmentServiceId,
                    PatientUserId = appointment.PatientUserId,
                    DentistUserId = dentistUserId,
                    SessionDate = DateOnly.FromDateTime(now),
                    Notes = notes,
                    ClinicalObservations = "Hoàn tất ca khám",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.TreatmentSessions.Add(session);
                await _dbContext.SaveChangesAsync(ct);
            }

            if (session != null)
            {
                var fuSchedule = new FollowUpSchedule
                {
                    TreatmentSessionId = session.SessionId,
                    PatientUserId = appointment.PatientUserId,
                    DentistUserId = dentistUserId,
                    RecommendedDate = DateOnly.FromDateTime(recommendedFollowUpDate ?? now.AddDays(7)),
                    RecommendedServiceId = primaryItem?.DepartmentServiceId,
                    ClinicalReason = string.IsNullOrWhiteSpace(followUpReason) ? "Tái khám theo dõi định kỳ sau thủ thuật" : followUpReason.Trim(),
                    Status = "Suggested",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.FollowUpSchedules.Add(fuSchedule);
            }
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Completed;
        appointment.CompletedAt = now;
        appointment.UpdatedAt = now;

        foreach (var item in appointment.AppointmentServices)
        {
            item.Status = AppointmentStatus.Completed;
            item.UpdatedAt = now;
        }

        AddStatusAuditLog(appointment.AppointmentId, oldStatus, AppointmentStatus.Completed, dentistUserId, string.IsNullOrWhiteSpace(notes) ? "Bác sĩ đã hoàn tất ca khám và điều trị." : notes.Trim(), now);

        var linkedFollowUp = await _dbContext.FollowUpSchedules
            .FirstOrDefaultAsync(f => f.ResultingAppointmentId == appointment.AppointmentId, ct);
        if (linkedFollowUp != null)
        {
            linkedFollowUp.Status = "Completed";
            linkedFollowUp.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(ct);

        var queueDto = await MapToDtoAsync(appointment.AppointmentId, ct);
        if (_queueHubContext != null && queueDto != null)
        {
            _ = _queueHubContext.Clients.All.ReceiveTreatmentCompleted(queueDto);
        }
        return ApiResponse.Ok("Đã hoàn thành ca khám thành công.");
    }

    public async Task<ApiResponse<List<ChairOptionDto>>> GetDentistRoomChairsAsync(
        long appointmentId,
        long dentistUserId,
        CancellationToken ct = default)
    {
        var apptItem = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(s => s.AppointmentId == appointmentId)
            .OrderBy(s => s.ScheduledStart)
            .FirstOrDefaultAsync(ct);

        if (apptItem == null)
        {
            return ApiResponse<List<ChairOptionDto>>.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        int targetRoomId = apptItem.RoomId ?? 0;
        if (targetRoomId <= 0)
        {
            var defaultRoom = await _dbContext.Rooms
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.DepartmentId == apptItem.DepartmentId && r.Status == "Active", ct);
            if (defaultRoom != null) targetRoomId = defaultRoom.RoomId;
        }

        if (targetRoomId <= 0)
        {
            return ApiResponse<List<ChairOptionDto>>.Ok(new List<ChairOptionDto>());
        }

        var chairs = await _dbContext.DentalChairs
            .AsNoTracking()
            .Where(c => c.RoomId == targetRoomId && c.Status == "Active")
            .OrderBy(c => c.ChairCode)
            .ToListAsync(ct);

        var activeStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var occupiedChairIds = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.AppointmentId != appointmentId &&
                        a.DentalChairId != null &&
                        activeStatuses.Contains(a.Status) &&
                        a.ScheduledStart < apptItem.ScheduledEnd &&
                        a.ScheduledEnd > apptItem.ScheduledStart)
            .Select(a => a.DentalChairId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var result = chairs.Select(c => new ChairOptionDto
        {
            DentalChairId = c.DentalChairId,
            RoomId = c.RoomId,
            ChairCode = c.ChairCode,
            Status = c.Status,
            IsOccupied = occupiedChairIds.Contains(c.DentalChairId)
        }).ToList();

        return ApiResponse<List<ChairOptionDto>>.Ok(result);
    }

    public async Task<ApiResponse> DentistAssignChairAsync(
        long appointmentId,
        long dentistUserId,
        long dentalChairId,
        CancellationToken ct = default)
    {
        var appt = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)
        {
            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        var primaryItem = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse.Fail("Ca khám chưa có dịch vụ liên kết.");
        }

        var chair = await _dbContext.DentalChairs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.DentalChairId == dentalChairId && c.Status == "Active", ct);
        if (chair == null)
        {
            return ApiResponse.Fail("Ghế nha khoa không tồn tại hoặc đã ngừng hoạt động.");
        }

        var now = DateTime.UtcNow;
        primaryItem.DentalChairId = chair.DentalChairId;
        primaryItem.RoomId = chair.RoomId;
        primaryItem.UpdatedAt = now;

        var oldStatus = appt.Status;
        if (appt.Status == AppointmentStatus.CheckedIn)
        {
            appt.Status = AppointmentStatus.InService;
            primaryItem.Status = AppointmentStatus.InService;
        }
        appt.UpdatedAt = now;

        AddStatusAuditLog(appointmentId, oldStatus, appt.Status, dentistUserId, $"Bác sĩ chọn ghế điều trị {chair.ChairCode}.", now);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Đã chọn ghế {chair.ChairCode} cho ca khám thành công.");
    }

    public Task<ApiResponse<AppointmentDto>> ReceptionistConfirmAsync(long appointmentId, long receptionistUserId, ManagerConfirmRequest request, CancellationToken ct = default)
        => ManagerConfirmAsync(appointmentId, receptionistUserId, request, ct);

    public Task<ApiResponse> ReceptionistProposeChangeAsync(long appointmentId, long receptionistUserId, ManagerProposeChangeRequest request, CancellationToken ct = default)
        => ManagerProposeChangeAsync(appointmentId, receptionistUserId, request, ct);

    public async Task<ApiResponse> DentistAddAdditionalServiceAsync(
        long appointmentId,
        long dentistUserId,
        DentistAddAdditionalServiceRequest request,
        CancellationToken ct = default)
    {
        if (request == null || request.ServiceId <= 0)
        {
            return ApiResponse.Fail("Vui lòng chọn dịch vụ muốn bổ sung.");
        }

        var appt = await _dbContext.Appointments
            .Include(a => a.AppointmentServices)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)
        {
            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        var primaryItem = appt.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse.Fail("Ca khám chưa có dịch vụ liên kết.");
        }

        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.ServiceId && s.Status == "Active", ct);

        if (service == null)
        {
            return ApiResponse.Fail("Dịch vụ không tồn tại hoặc đã ngừng hoạt động.");
        }

        var now = DateTime.UtcNow;
        var session = appt.TreatmentSessions.FirstOrDefault();
        if (session == null)
        {
            session = new TreatmentSession
            {
                AppointmentId = appointmentId,
                AppointmentServiceId = primaryItem.AppointmentServiceId,
                PatientUserId = appt.PatientUserId,
                DentistUserId = dentistUserId,
                SessionDate = DateOnly.FromDateTime(now),
                Notes = $"Phiên điều trị cho ca khám APT-{appt.AppointmentId:D6}",
                ClinicalObservations = "Thực hiện dịch vụ nha khoa",
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.TreatmentSessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);
        }

        var qty = request.Quantity > 0 ? request.Quantity : 1;
        _dbContext.PerformedServices.Add(new PerformedService
        {
            TreatmentSessionId = session.SessionId,
            AppointmentId = appointmentId,
            AppointmentServiceId = primaryItem.AppointmentServiceId,
            DepartmentServiceId = service.DepartmentServiceId,
            Quantity = qty,
            UnitPrice = service.Price,
            Notes = request.Notes,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok($"Đã bổ sung dịch vụ {service.ServiceName} vào ca khám thành công!");
    }

    public async Task<ApiResponse<List<BookingComboDto>>> GetAvailableCombosAsync(CancellationToken ct = default)
    {
        var combos = await _dbContext.ServiceCombos
            .AsNoTracking()
            .Include(c => c.ServiceComboItems)
                .ThenInclude(ci => ci.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Where(c => c.IsActive)
            .OrderBy(c => c.ComboId)
            .ToListAsync(ct);

        var result = combos.Select(c =>
        {
            var firstItem = c.ServiceComboItems.FirstOrDefault();
            return new BookingComboDto
            {
                ComboId = c.ComboId,
                ComboCode = c.ComboCode,
                Title = c.ComboName,
                Description = c.Description ?? string.Empty,
                DepartmentId = firstItem?.DepartmentService?.DepartmentId ?? 1,
                DepartmentName = firstItem?.DepartmentService?.Department?.Name ?? string.Empty,
                PrimaryServiceId = firstItem?.DepartmentServiceId ?? 1,
                PrimaryServiceName = firstItem?.DepartmentService?.ServiceName ?? string.Empty,
                OriginalPrice = c.TotalOriginalPrice,
                ComboPrice = c.DiscountedPrice,
                DurationMinutes = c.ServiceComboItems.Sum(ci => ci.DepartmentService?.DurationMinutes ?? 30),
                IncludedServices = c.ServiceComboItems
                    .Where(ci => ci.DepartmentService != null)
                    .Select(ci => ci.DepartmentService.ServiceName)
                    .ToList(),
                Benefits = new List<string>(),
                Status = c.IsActive ? "Active" : "Inactive"
            };
        }).ToList();

        return ApiResponse<List<BookingComboDto>>.Ok(result);
    }

    private void AddStatusAuditLog(long appointmentId, string? oldStatus, string newStatus, long? changedByUserId, string? note, DateTime changedAt)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = changedByUserId,
            Action = "APPOINTMENT_STATUS_CHANGE",
            EntityName = "Appointment",
            EntityId = appointmentId.ToString(),
            OldValues = oldStatus,
            NewValues = $"{newStatus}|{note ?? string.Empty}",
            CreatedAt = changedAt
        });
    }

    private async Task<List<AppointmentDto>> MapToDtoListAsync(IQueryable<Appointment> query, CancellationToken ct)
    {
        var appointments = await query
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Include(a => a.Combo)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.RequestedDentist)
                    .ThenInclude(d => d!.StaffProfile)
                        .ThenInclude(sp => sp.User)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
                        .ThenInclude(sp => sp.User)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.Room)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DentalChair)
            .ToListAsync(ct);

        return appointments.Select(a => MapEntityToDto(a, new List<AppointmentChangeProposalDto>(), new List<StatusHistoryDto>())).ToList();
    }

    private async Task<AppointmentDto?> MapToDtoAsync(long appointmentId, CancellationToken ct)
    {
        var a = await _dbContext.Appointments
            .AsNoTracking()
            .Include(ap => ap.Patient)
                .ThenInclude(p => p.User)
            .Include(ap => ap.Combo)
            .Include(ap => ap.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(ap => ap.AppointmentServices)
                .ThenInclude(s => s.RequestedDentist)
                    .ThenInclude(d => d!.StaffProfile)
                        .ThenInclude(sp => sp.User)
            .Include(ap => ap.AppointmentServices)
                .ThenInclude(s => s.AssignedDentist)
                    .ThenInclude(d => d!.StaffProfile)
                        .ThenInclude(sp => sp.User)
            .Include(ap => ap.AppointmentServices)
                .ThenInclude(s => s.Room)
            .Include(ap => ap.AppointmentServices)
                .ThenInclude(s => s.DentalChair)
            .FirstOrDefaultAsync(ap => ap.AppointmentId == appointmentId, ct);

        if (a == null) return null;

        var auditRows = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(h => h.User)
                .ThenInclude(u => u!.StaffProfile)
            .Include(h => h.User)
                .ThenInclude(u => u!.PatientProfile)
            .Where(h => h.EntityName == "Appointment" && h.EntityId == appointmentId.ToString())
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var histories = auditRows.Select(h =>
        {
            var parts = (h.NewValues ?? string.Empty).Split('|', 2);
            var newStatus = parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]) ? parts[0] : a.Status;
            var note = parts.Length > 1 ? parts[1] : h.NewValues;
            return new StatusHistoryDto
            {
                HistoryId = h.AuditLogId,
                AppointmentId = appointmentId,
                OldStatus = h.OldValues,
                NewStatus = newStatus,
                ChangedByUserId = h.UserId,
                ChangedByName = h.User?.StaffProfile?.FullName ?? h.User?.PatientProfile?.FullName ?? h.User?.Email,
                Note = note,
                ChangedAt = h.CreatedAt
            };
        }).ToList();

        return MapEntityToDto(a, new List<AppointmentChangeProposalDto>(), histories);
    }

    private static AppointmentDto MapEntityToDto(
        Appointment a,
        List<AppointmentChangeProposalDto> proposals,
        List<StatusHistoryDto> histories)
    {
        var s = a.AppointmentServices.OrderBy(item => item.ScheduledStart).FirstOrDefault();
        var queueCode = s?.QueueNumber.HasValue == true
            ? $"Q-{s.DepartmentId:D2}-{s.QueueNumber.Value:D3}"
            : null;

        return new AppointmentDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = $"APT-{a.AppointmentId:D6}",
            PatientUserId = a.PatientUserId,
            PatientName = a.Patient?.FullName ?? string.Empty,
            PatientCode = $"BN-{a.PatientUserId:D4}",
            PatientPhone = a.Patient?.User?.PhoneNumber,
            DepartmentId = s?.DepartmentId,
            DepartmentName = s?.DepartmentService?.Department?.Name,
            RequestedServiceId = s?.DepartmentServiceId,
            ServiceName = s?.DepartmentService?.ServiceName,
            ServiceDuration = s?.DepartmentService?.DurationMinutes,
            ServicePrice = s?.UnitPrice ?? s?.DepartmentService?.Price,
            RequestedDentistUserId = s?.RequestedDentistUserId,
            RequestedDentistName = s?.RequestedDentist?.StaffProfile?.FullName,
            AssignedDentistUserId = s?.AssignedDentistUserId,
            AssignedDentistName = s?.AssignedDentist?.StaffProfile?.FullName,
            RoomId = s?.RoomId,
            RoomName = s?.Room?.RoomName ?? s?.Room?.RoomCode,
            DentalChairId = s?.DentalChairId,
            ChairCode = s?.DentalChair?.ChairCode,
            ScheduledStart = s?.ScheduledStart,
            ScheduledEnd = s?.ScheduledEnd,
            ComboCode = a.Combo?.ComboCode,
            ComboId = a.ComboId,
            ComboTitle = a.Combo?.ComboName,
            ReasonForVisit = a.ReasonForVisit ?? string.Empty,
            BookingSource = a.BookingSource,
            QueueNumber = queueCode,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            ConfirmedAt = a.ConfirmedAt,
            CheckedInAt = a.CheckedInAt,
            CompletedAt = a.CompletedAt,
            CancelledAt = a.CancelledAt,
            CancellationReason = a.CancellationReason,
            UpdatedAt = a.UpdatedAt,
            Proposals = proposals,
            StatusHistories = histories
        };
    }
}
