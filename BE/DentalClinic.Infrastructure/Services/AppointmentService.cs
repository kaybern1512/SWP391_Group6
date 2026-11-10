using DentalClinic.Domain.Common;

using System;

using System.Collections.Generic;

using System.Linq;

using System.Security.Cryptography;

using System.Threading;

using System.Threading.Tasks;

using DentalClinic.Application.Common.Interfaces;

using DentalClinic.Application.Common.Models;

using DentalClinic.Application.Features.Admin.DTOs;

using DentalClinic.Application.Features.Appointments.DTOs;

using DentalClinic.Application.Features.Booking.DTOs;

using DentalClinic.Domain.Enums;

using DentalClinic.Infrastructure.Persistence;

using DentalClinic.Infrastructure.Persistence.Entities;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AppointmentService : IAppointmentService

{

    private readonly DentalClinicDbContext _dbContext;

    private readonly INotificationService _notificationService;

    private readonly IEmailService? _emailService;

    private readonly IAppointmentReminderService? _reminderService;

    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(

        DentalClinicDbContext dbContext,

        INotificationService notificationService,

        ILogger<AppointmentService> logger)

        : this(dbContext, notificationService, null, null, logger)

    {

    }

    public AppointmentService(

        DentalClinicDbContext dbContext,

        INotificationService notificationService,

        IEmailService? emailService,

        ILogger<AppointmentService> logger)

        : this(dbContext, notificationService, emailService, null, logger)

    {

    }

    public AppointmentService(

        DentalClinicDbContext dbContext,

        INotificationService notificationService,

        IEmailService? emailService,

        IAppointmentReminderService? reminderService,

        ILogger<AppointmentService> logger)

    {

        _dbContext = dbContext;

        _notificationService = notificationService;

        _emailService = emailService;

        _reminderService = reminderService;

        _logger = logger;

    }

    public async Task<ApiResponse<AppointmentDto>> CreatePatientAppointmentAsync(

        long patientUserId,

        CreateAppointmentRequest request,

        CancellationToken ct = default)

    {

        // 0. Check Booking Eligibility under Strike Policy

        var eligibility = await GetBookingEligibilityAsync(patientUserId, ct);

        if (eligibility.Success && eligibility.Data != null && !eligibility.Data.IsEligible)

        {

            return ApiResponse<AppointmentDto>.Fail(eligibility.Data.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt lịch trực tuyến do vi phạm chính sách hủy lịch.");

        }

        // 1. Validate Patient

        var patient = await _dbContext.PatientProfiles

            .Include(p => p.User)

            .FirstOrDefaultAsync(p => p.UserId == patientUserId, ct);

        if (patient == null || patient.User.Status != AccountStatus.Active)

        {

            return ApiResponse<AppointmentDto>.Fail("Tài khoản bệnh nhân không hợp lệ hoặc chưa được kích hoạt.");

        }

                // 1b. Validate & Map Service Combo if specified
        ServiceCombo? selectedCombo = null;
        if (request.ComboId.HasValue && request.ComboId.Value > 0)
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComboId == request.ComboId.Value && c.Status == "Active", ct);
            if (selectedCombo == null)
            {
                return ApiResponse<AppointmentDto>.Fail("GÃ³i Combo dá»‹ch vá»¥ khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng Ã¡p dá»¥ng.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ComboCode))
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComboCode == request.ComboCode && c.Status == "Active", ct);
            if (selectedCombo == null)
            {
                return ApiResponse<AppointmentDto>.Fail("GÃ³i Combo dá»‹ch vá»¥ khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng Ã¡p dá»¥ng.");
            }
        }

        if (selectedCombo != null)
        {
            request.DepartmentId = selectedCombo.DepartmentId;
            request.RequestedServiceId = selectedCombo.PrimaryServiceId;
        }

        // 2. Validate Department

        var department = await _dbContext.Departments

            .AsNoTracking()

            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.Status == "Active", ct);

        if (department == null)

        {

            return ApiResponse<AppointmentDto>.Fail("Chuyên khoa không tồn tại hoặc đã ngừng hoạt động.");

        }

        // 3. Validate Service

        var service = await _dbContext.DepartmentServices

            .AsNoTracking()

            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.RequestedServiceId &&

                                      s.DepartmentId == request.DepartmentId &&

                                      s.Status == "Active", ct);

        if (service == null)

        {

            return ApiResponse<AppointmentDto>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa này.");

        }

        // 4. Validate Datetime & Lead Time

        var requestedStart = ClinicTimeHelper.ToClinicTime(request.ScheduledStart);

        var clinicNow = ClinicTimeHelper.Now;

        if (requestedStart <= clinicNow)

        {

            return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");

        }

        if (service.MinLeadTimeHours > 0)

        {

            if (requestedStart < clinicNow.AddHours(service.MinLeadTimeHours).AddMinutes(-1))

            {

                return ApiResponse<AppointmentDto>.Fail($"Dịch vụ '{service.ServiceName}' yêu cầu đặt lịch trước tối thiểu {service.MinLeadTimeHours} giờ.");

            }

        }

        // 5. Validate Requested Dentist if specified

        if (request.RequestedDentistUserId.HasValue)

        {

            var isDentistInDept = await _dbContext.DentistDepartments

                .AsNoTracking()

                .AnyAsync(dd => dd.DentistUserId == request.RequestedDentistUserId.Value &&

                                dd.DepartmentId == request.DepartmentId &&

                                dd.Status == "Active", ct);

            if (!isDentistInDept)

            {

                return ApiResponse<AppointmentDto>.Fail("Bác sĩ được yêu cầu không thuộc chuyên khoa đã chọn.");

            }

        }

        // Calculate end time

        var duration = service.DurationMinutes.HasValue && service.DurationMinutes.Value > 0

            ? service.DurationMinutes.Value

            : 30;

        var scheduledEnd = requestedStart.AddMinutes(duration);

        // 5b. Prevent Duplicate Booking (WF-02 Chống tạo yêu cầu trùng do bấm gửi nhiều lần)

        var targetDateStart = requestedStart.Date;

        var targetDateEnd = targetDateStart.AddDays(1);

        var existingSameDayAppointments = await _dbContext.Appointments

            .AsNoTracking()

            .Where(a => a.PatientUserId == patientUserId &&

                        a.ScheduledStart >= targetDateStart &&

                        a.ScheduledStart < targetDateEnd &&

                        (a.Status == AppointmentStatus.PendingReceptionReview ||

                         a.Status == AppointmentStatus.PendingDepartmentReview ||

                         a.Status == AppointmentStatus.Confirmed ||

                         a.Status == AppointmentStatus.InService))

            .ToListAsync(ct);

        var duplicateExists = existingSameDayAppointments.Any(a =>

            a.RequestedServiceId == request.RequestedServiceId &&

            a.ScheduledStart == requestedStart);

        if (duplicateExists)

        {

            return ApiResponse<AppointmentDto>.Fail("Bạn đã gửi một yêu cầu đặt lịch khám cho dịch vụ và khung giờ này rồi. Vui lòng kiểm tra lại trong Lịch hẹn của tôi.");

        }

        // 5c. Shift Policy: Tối đa 2 lịch khám trong 1 ngày và PHẢI ở 2 buổi khác nhau (1 buổi Sáng, 1 buổi Chiều/Tối)

        if (existingSameDayAppointments.Count >= 2)

        {

            return ApiResponse<AppointmentDto>.Fail(

                $"Theo quy định phòng khám, mỗi bệnh nhân chỉ được đặt tối đa 2 lịch khám trong cùng một ngày (1 buổi sáng và 1 buổi chiều). Bạn đã có {existingSameDayAppointments.Count} lịch hẹn vào ngày {requestedStart:dd/MM/yyyy}. Vui lòng chọn ngày khám khác.");

        }

        var isNewAppointmentMorning = requestedStart.TimeOfDay < new TimeSpan(12, 0, 0);

        foreach (var existing in existingSameDayAppointments)
        {
            if (!existing.ScheduledStart.HasValue) continue;
            var existingStart = existing.ScheduledStart.Value;
            var existingEnd = existing.ScheduledEnd ?? existingStart.AddMinutes(30);

            // Kiểm tra giao thoa khoảng thời gian (Time Overlap)
            if (requestedStart < existingEnd && scheduledEnd > existingStart)
            {
                return ApiResponse<AppointmentDto>.Fail(
                    $"Bạn đã có lịch khám #{existing.AppointmentCode} từ {existingStart:HH:mm} đến {existingEnd:HH:mm} cùng ngày. Vui lòng chọn khung giờ không bị trùng.");
            }

            // Kiểm tra buổi (Phải ở 2 buổi khác nhau)
            var isExistingMorning = existingStart.TimeOfDay < new TimeSpan(12, 0, 0);
            if (isNewAppointmentMorning && isExistingMorning)
            {
                return ApiResponse<AppointmentDto>.Fail(
                    $"Bạn đã có một lịch khám vào Buổi Sáng ngày {requestedStart:dd/MM/yyyy} (lúc {existingStart:HH:mm}). Phòng khám quy định chỉ được đặt tối đa 2 lịch/ngày nếu ở 2 buổi khác nhau. Vui lòng chọn khung giờ vào Buổi Chiều.");
            }

            if (!isNewAppointmentMorning && !isExistingMorning)
            {
                return ApiResponse<AppointmentDto>.Fail(
                    $"Bạn đã có một lịch khám vào Buổi Chiều ngày {requestedStart:dd/MM/yyyy} (lúc {existingStart:HH:mm}). Phòng khám quy định chỉ được đặt tối đa 2 lịch/ngày nếu ở 2 buổi khác nhau. Vui lòng chọn khung giờ vào Buổi Sáng.");
            }
        }

        // Generate Unique AppointmentCode (APP-yyyyMMdd-XXXXXX)

        var code = await GenerateUniqueAppointmentCodeAsync(ct);

        var now = DateTime.UtcNow;

        var appointment = new Appointment

        {

            AppointmentCode = code,

            PatientUserId = patientUserId,

            DepartmentId = request.DepartmentId,

            RequestedServiceId = request.RequestedServiceId,

            RequestedDentistUserId = request.RequestedDentistUserId,

            AssignedDentistUserId = null, // NEVER assign at patient booking stage!

            RoomId = null,

            DentalChairId = null,

            ScheduledStart = requestedStart,

            ScheduledEnd = scheduledEnd,
            ComboCode = selectedCombo != null ? selectedCombo.ComboCode : request.ComboCode,
            ComboId = selectedCombo != null ? selectedCombo.ComboId : request.ComboId,

            ReasonForVisit = string.IsNullOrWhiteSpace(request.ReasonForVisit) ? "Khám bệnh" : request.ReasonForVisit.Trim(),

            BookingSource = BookingSource.Patient,

            QueueNumber = null,

            Status = AppointmentStatus.PendingReceptionReview,

            CreatedByUserId = patientUserId,

            CreatedAt = now

        };

        _dbContext.Appointments.Add(appointment);

        await _dbContext.SaveChangesAsync(ct);

        // Record Status History

        var history = new AppointmentStatusHistory

        {

            AppointmentId = appointment.AppointmentId,

            OldStatus = null,

            NewStatus = AppointmentStatus.PendingReceptionReview,

            ChangedByUserId = patientUserId,

            Note = "Bệnh nhân gửi yêu cầu đặt lịch khám.",

            ChangedAt = now

        };

        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        // Notification to Patient

        await _notificationService.CreateNotificationAsync(

            patientUserId,

            appointment.AppointmentId,

            "AppointmentSubmitted",

            "Yêu cầu đặt lịch đã được gửi",

            $"Yêu cầu đặt lịch mã {appointment.AppointmentCode} đã được gửi thành công. Phòng khám sẽ xem xét và phản hồi sớm nhất.",

            ct);

        var dto = await MapToDtoAsync(appointment.AppointmentId, ct);

        return ApiResponse<AppointmentDto>.Ok(dto!, "Yêu cầu đặt lịch khám đã được gửi thành công. Vui lòng chờ phòng khám xem xét.");

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

                (u.PatientProfile != null && u.PatientProfile.FullName.ToLower().Contains(q)) ||

                (u.PatientProfile != null && u.PatientProfile.PatientCode.ToLower().Contains(q)));

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

                FullName = u.PatientProfile != null ? u.PatientProfile.FullName : "N/A",

                Code = u.PatientProfile != null ? u.PatientProfile.PatientCode : null,

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

        if (request == null)

        {

            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Thông tin đăng ký không hợp lệ.");

        }

        if (string.IsNullOrWhiteSpace(request.FullName))

        {

            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Vui lòng nhập họ và tên bệnh nhân.");

        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))

        {

            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Vui lòng nhập số điện thoại bệnh nhân.");

        }

        var email = request.Email?.Trim().ToLowerInvariant();

        var phone = request.PhoneNumber.Trim();

        // 1. Prevent duplicate email if provided

        if (!string.IsNullOrWhiteSpace(email))

        {

            var existingEmailUser = await _dbContext.UserAccounts

                .Include(u => u.PatientProfile)

                .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email, ct);

            if (existingEmailUser != null)

            {

                return ApiResponse<RegisterWalkInPatientResponse>.Fail("Email này đã tồn tại trong hệ thống. Vui lòng tìm kiếm hồ sơ bệnh nhân để đặt lịch.");

            }

        }

        // 2. Prevent duplicate phone number

        var existingPhoneUser = await _dbContext.UserAccounts

            .Include(u => u.PatientProfile)

            .FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);

        if (existingPhoneUser != null)

        {

            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Số điện thoại này đã được đăng ký với một hồ sơ khác.");

        }

        // 3. Generate monotonic PatientCode

        var now = DateTime.UtcNow;

        var yearMonth = now.ToString("yyyyMM");

        var prefix = $"PAT-{yearMonth}-";

        var lastCode = await _dbContext.PatientProfiles

            .Where(p => p.PatientCode.StartsWith(prefix))

            .OrderByDescending(p => p.PatientCode)

            .Select(p => p.PatientCode)

            .FirstOrDefaultAsync(ct);

        int nextSeq = 1;

        if (!string.IsNullOrEmpty(lastCode) && lastCode.Length >= prefix.Length + 4)

        {

            var seqStr = lastCode.Substring(prefix.Length);

            if (int.TryParse(seqStr, out var currentSeq))

            {

                nextSeq = currentSeq + 1;

            }

        }

        var patientCode = $"{prefix}{nextSeq:D4}";

        // 4. Create UserAccount and PatientProfile (Secure administrative profile without hardcoded password)

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;

        if (_dbContext.Database.IsRelational())

        {

            tx = await _dbContext.Database.BeginTransactionAsync(ct);

        }

        try

        {

            var userAccount = new UserAccount

            {

                Email = string.IsNullOrWhiteSpace(email) ? null : email,

                PhoneNumber = phone,

                PasswordHash = null, // No hard-coded password! Patient must activate / set password securely.

                Role = UserRole.Patient,

                Status = AccountStatus.Unverified, // Administrative profile awaiting online activation

                CreatedAt = now,

                FailedLoginCount = 0

            };

            _dbContext.UserAccounts.Add(userAccount);

            await _dbContext.SaveChangesAsync(ct);

            var normalizedGender = (request.Gender?.Trim().ToLowerInvariant()) switch

            {

                "nam" or "male" => "Male",

                "nữ" or "nu" or "female" => "Female",

                _ => "Other"

            };

            var patientProfile = new PatientProfile

            {

                UserId = userAccount.UserId,

                PatientCode = patientCode,

                FullName = request.FullName.Trim(),

                DateOfBirth = request.DateOfBirth.HasValue ? DateOnly.FromDateTime(request.DateOfBirth.Value) : null,

                Gender = normalizedGender,

                CreatedAt = now

            };

            _dbContext.PatientProfiles.Add(patientProfile);

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)

            {

                await tx.CommitAsync(ct);

            }

            var response = new RegisterWalkInPatientResponse

            {

                PatientUserId = userAccount.UserId,

                PatientCode = patientCode,

                FullName = patientProfile.FullName,

                Email = userAccount.Email ?? string.Empty,

                PhoneNumber = userAccount.PhoneNumber ?? string.Empty,

                Status = userAccount.Status,

                Message = "Tiếp nhận hồ sơ bệnh nhân thành công. Tài khoản trực tuyến ở trạng thái chờ kích hoạt."

            };

            return ApiResponse<RegisterWalkInPatientResponse>.Ok(response, "Tiếp nhận hồ sơ bệnh nhân thành công.");

        }

        catch (Exception ex)

        {

            if (tx != null)

            {

                await tx.RollbackAsync(ct);

            }

            _logger.LogError(ex, "Lỗi khi tiếp nhận hồ sơ bệnh nhân vãng lai");

            return ApiResponse<RegisterWalkInPatientResponse>.Fail("Đã xảy ra lỗi trong quá trình tiếp nhận hồ sơ bệnh nhân.");

        }

        finally

        {

            if (tx != null)

            {

                await tx.DisposeAsync();

            }

        }

    }

    public async Task<ApiResponse<AppointmentDto>> CreateReceptionistAppointmentAsync(

        long receptionistUserId,

        CreateReceptionistAppointmentRequest request,

        CancellationToken ct = default)

    {

        if (request == null)

        {

            return ApiResponse<AppointmentDto>.Fail("Dữ liệu đặt lịch không hợp lệ.");

        }

        if (request.PatientUserId <= 0)

        {

            return ApiResponse<AppointmentDto>.Fail("Vui lòng chọn bệnh nhân.");

        }

        // 1. Validate Patient (Active online account or Unverified walk-in administrative record)

        var patient = await _dbContext.PatientProfiles

            .Include(p => p.User)

            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null || patient.User.Status == AccountStatus.Locked || patient.User.Status == AccountStatus.Inactive)

        {

            return ApiResponse<AppointmentDto>.Fail("Hồ sơ bệnh nhân không hợp lệ hoặc đã bị khóa.");

        }

                // 1b. Validate & Map Service Combo if specified
        ServiceCombo? selectedCombo = null;
        if (request.ComboId.HasValue && request.ComboId.Value > 0)
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComboId == request.ComboId.Value && c.Status == "Active", ct);
            if (selectedCombo == null)
            {
                return ApiResponse<AppointmentDto>.Fail("GÃ³i Combo dá»‹ch vá»¥ khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng Ã¡p dá»¥ng.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.ComboCode))
        {
            selectedCombo = await _dbContext.ServiceCombos
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ComboCode == request.ComboCode && c.Status == "Active", ct);
            if (selectedCombo == null)
            {
                return ApiResponse<AppointmentDto>.Fail("GÃ³i Combo dá»‹ch vá»¥ khÃ´ng tá»“n táº¡i hoáº·c Ä‘Ã£ ngá»«ng Ã¡p dá»¥ng.");
            }
        }

        if (selectedCombo != null)
        {
            request.DepartmentId = selectedCombo.DepartmentId;
            request.RequestedServiceId = selectedCombo.PrimaryServiceId;
        }

        // 2. Validate Department

        var department = await _dbContext.Departments

            .AsNoTracking()

            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.Status == "Active", ct);

        if (department == null)

        {

            return ApiResponse<AppointmentDto>.Fail("Chuyên khoa không tồn tại hoặc đã ngừng hoạt động.");

        }

        // 3. Validate Service

        var service = await _dbContext.DepartmentServices

            .AsNoTracking()

            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.RequestedServiceId &&

                                      s.DepartmentId == request.DepartmentId &&

                                      s.Status == "Active", ct);

        if (service == null)

        {

            return ApiResponse<AppointmentDto>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa này.");

        }

        // 4. Validate Datetime

        var requestedStart = ClinicTimeHelper.ToClinicTime(request.ScheduledStart);

        var clinicNow = ClinicTimeHelper.Now;

        if (requestedStart < clinicNow.AddMinutes(-10))

        {

            return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");

        }

        // 5. Validate Requested Dentist if specified

        if (request.RequestedDentistUserId.HasValue && request.RequestedDentistUserId.Value > 0)

        {

            var isDentistInDept = await _dbContext.DentistDepartments

                .AsNoTracking()

                .AnyAsync(dd => dd.DentistUserId == request.RequestedDentistUserId.Value &&

                                dd.DepartmentId == request.DepartmentId &&

                                dd.Status == "Active", ct);

            if (!isDentistInDept)

            {

                return ApiResponse<AppointmentDto>.Fail("Bác sĩ được yêu cầu không thuộc chuyên khoa đã chọn.");

            }

        }

        else

        {

            request.RequestedDentistUserId = null;

        }

        // 5b. Prevent Duplicate Booking (WF-02 Chống tạo yêu cầu trùng do bấm gửi nhiều lần)

        var duplicateExists = await _dbContext.Appointments

            .AsNoTracking()

            .AnyAsync(a => a.PatientUserId == request.PatientUserId &&

                           a.RequestedServiceId == request.RequestedServiceId &&

                           a.ScheduledStart == requestedStart &&

                           (a.Status == AppointmentStatus.PendingReceptionReview ||

                            a.Status == AppointmentStatus.PendingDepartmentReview ||

                            a.Status == AppointmentStatus.Confirmed), ct);

        if (duplicateExists)

        {

            return ApiResponse<AppointmentDto>.Fail("Bạn đã gửi một yêu cầu đặt lịch khám cho dịch vụ và khung giờ này rồi. Vui lòng kiểm tra lại trong Lịch hẹn của tôi.");

        }

        // Calculate end time

        var duration = service.DurationMinutes.HasValue && service.DurationMinutes.Value > 0

            ? service.DurationMinutes.Value

            : 30;

        var scheduledEnd = requestedStart.AddMinutes(duration);

        // Determine if this request should be auto-confirmed (walk-in intake with resource assignment)

        bool hasAllResources = request.AssignedDentistUserId.HasValue && request.AssignedDentistUserId.Value > 0 &&

                               request.RoomId.HasValue && request.RoomId.Value > 0 &&

                               request.DentalChairId.HasValue && request.DentalChairId.Value > 0;

        bool shouldConfirm = request.AutoConfirm || hasAllResources;

        if (request.AutoConfirm && !hasAllResources)

        {

            return ApiResponse<AppointmentDto>.Fail("Vui lòng phân bổ đầy đủ Bác sĩ, Phòng khám và Ghế nha khoa để xác nhận lịch hẹn tại quầy.");

        }

        long? assignedDentistId = null;

        long? roomId = null;

        long? dentalChairId = null;

        string? queueNumber = null;

        var now = DateTime.UtcNow;

        IDisposable? lockScope = null;

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;

        bool committed = false;

        try

        {

            if (shouldConfirm)

            {

                assignedDentistId = request.AssignedDentistUserId!.Value;

                roomId = request.RoomId!.Value;

                dentalChairId = request.DentalChairId!.Value;

                // 1. Validate assigned dentist in department

                var isAssignedInDept = await _dbContext.DentistDepartments

                    .AsNoTracking()

                    .AnyAsync(dd => dd.DentistUserId == assignedDentistId.Value &&

                                    dd.DepartmentId == request.DepartmentId &&

                                    dd.Status == "Active", ct);

                if (!isAssignedInDept)

                {

                    return ApiResponse<AppointmentDto>.Fail("Bác sĩ được chỉ định không thuộc chuyên khoa đã chọn.");

                }

                // 2. Validate Room

                var room = await _dbContext.Rooms

                    .AsNoTracking()

                    .FirstOrDefaultAsync(r => r.RoomId == roomId.Value && r.Status == "Active", ct);

                if (room == null)

                {

                    return ApiResponse<AppointmentDto>.Fail("Phòng khám không tồn tại hoặc đã ngừng hoạt động.");

                }

                if (room.DepartmentId.HasValue && room.DepartmentId.Value != request.DepartmentId)

                {

                    return ApiResponse<AppointmentDto>.Fail("Phòng khám không thuộc chuyên khoa đã chọn.");

                }

                // 3. Validate Dental Chair

                var chair = await _dbContext.DentalChairs

                    .AsNoTracking()

                    .FirstOrDefaultAsync(c => c.DentalChairId == dentalChairId.Value &&

                                              c.RoomId == roomId.Value &&

                                              c.Status == "Active", ct);

                if (chair == null)

                {

                    return ApiResponse<AppointmentDto>.Fail("Ghế nha khoa không thuộc phòng khám đã chọn hoặc không hoạt động.");

                }

                // 4. Acquire Concurrency Locks

                var targetDate = requestedStart.Date;

                var lockKeys = new List<string>

                {

                    SqlAppLockHelper.DentistMasterKey(assignedDentistId.Value),

                    SqlAppLockHelper.DentistKey(assignedDentistId.Value, targetDate),

                    SqlAppLockHelper.PatientKey(request.PatientUserId, targetDate),

                    SqlAppLockHelper.DeptQueueKey(request.DepartmentId, targetDate),

                    SqlAppLockHelper.ChairKey(dentalChairId.Value, targetDate)

                };

                if (room.IsExclusive)

                {

                    lockKeys.Add(SqlAppLockHelper.RoomKey(roomId.Value, targetDate));

                }

                if (_dbContext.Database.IsRelational())

                {

                    tx = await _dbContext.Database.BeginTransactionAsync(ct);

                }

                lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

                var occupiedStatuses = new[]

                {

                    AppointmentStatus.Confirmed,

                    AppointmentStatus.CheckedIn,

                    AppointmentStatus.Waiting,

                    AppointmentStatus.Called,

                    AppointmentStatus.InService

                };

                // 5. Validate Dentist Work Shift Coverage

                var targetDateOnly = DateOnly.FromDateTime(targetDate);

                var startTimeOnly = TimeOnly.FromDateTime(requestedStart);

                var endTimeOnly = TimeOnly.FromDateTime(scheduledEnd);

                var doctorShifts = await _dbContext.DentistWorkSchedules

                    .AsNoTracking()

                    .Where(ws => ws.DentistUserId == assignedDentistId.Value &&

                                 ws.WorkDate == targetDateOnly &&

                                 ws.DepartmentId == request.DepartmentId)

                    .ToListAsync(ct);

                if (!doctorShifts.Any())

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Bác sĩ không có ca làm việc trong ngày được chọn.");

                }

                var activeShifts = doctorShifts.Where(ws => ws.Status == "Scheduled").ToList();

                if (!activeShifts.Any())

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Ca làm việc của Bác sĩ đã bị hủy.");

                }

                var coveringShift = activeShifts.FirstOrDefault(ws => ws.StartTime <= startTimeOnly && ws.EndTime >= endTimeOnly);

                if (coveringShift == null)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    var shiftTimes = string.Join(", ", activeShifts.Select(s => $"{s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm}"));

                    return ApiResponse<AppointmentDto>.Fail($"Giờ khám nằm ngoài khung giờ làm việc của Bác sĩ (ca làm việc: {shiftTimes}).");

                }

                // 6. Validate Dentist Leave

                bool dentistLeaveConflict = await _dbContext.DentistAvailabilities

                    .AsNoTracking()

                    .AnyAsync(da => da.DentistUserId == assignedDentistId.Value &&

                                    da.AvailabilityStatus != DentistAvailabilityStatus.Available &&

                                    da.StartDateTime < scheduledEnd &&

                                    (da.EndDateTime == null || da.EndDateTime > requestedStart), ct);

                if (dentistLeaveConflict)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này.");

                }

                // 7. Validate Overlaps: Dentist, Patient, Chair, Exclusive Room

                bool dentistConflict = await _dbContext.Appointments

                    .AnyAsync(a => a.AssignedDentistUserId == assignedDentistId.Value &&

                                   occupiedStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < scheduledEnd &&

                                   a.ScheduledEnd > requestedStart, ct);

                if (dentistConflict)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Bác sĩ đã có lịch hẹn khác trong khoảng thời gian này.");

                }

                bool patientConflict = await _dbContext.Appointments

                    .AnyAsync(a => a.PatientUserId == request.PatientUserId &&

                                   occupiedStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < scheduledEnd &&

                                   a.ScheduledEnd > requestedStart, ct);

                if (patientConflict)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Bệnh nhân đã có lịch hẹn khác trong khoảng thời gian này.");

                }

                bool chairConflict = await _dbContext.Appointments

                    .AnyAsync(a => a.DentalChairId == dentalChairId.Value &&

                                   occupiedStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < scheduledEnd &&

                                   a.ScheduledEnd > requestedStart, ct);

                if (chairConflict)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Ghế nha khoa đã bị trùng lịch trong khoảng thời gian này.");

                }

                if (room.IsExclusive)

                {

                    bool roomConflict = await _dbContext.Appointments

                        .AnyAsync(a => a.RoomId == roomId.Value &&

                                       occupiedStatuses.Contains(a.Status) &&

                                       a.ScheduledStart < scheduledEnd &&

                                       a.ScheduledEnd > requestedStart, ct);

                    if (roomConflict)

                    {

                        if (tx != null) await tx.RollbackAsync(ct);

                        return ApiResponse<AppointmentDto>.Fail("Phòng khám này yêu cầu sử dụng độc quyền và hiện đang bận.");

                    }

                }

                // 8. Monotonic Queue Number Allocation

                var existingQueueNumbers = await _dbContext.Appointments

                    .Where(a => a.DepartmentId == request.DepartmentId &&

                                a.ScheduledStart != null &&

                                a.ScheduledStart.Value.Date == targetDate &&

                                a.QueueNumber != null &&

                                a.QueueNumber != "")

                    .Select(a => a.QueueNumber!)

                    .ToListAsync(ct);

                int maxSeq = 0;

                foreach (var qn in existingQueueNumbers)

                {

                    if (!string.IsNullOrEmpty(qn) && qn.StartsWith("Q") && int.TryParse(qn.Substring(1), out int num))

                    {

                        if (num > maxSeq) maxSeq = num;

                    }

                }

                queueNumber = $"Q{(maxSeq + 1):D3}";

            }

            var code = await GenerateUniqueAppointmentCodeAsync(ct);

            var appointment = new Appointment

            {

                AppointmentCode = code,

                PatientUserId = request.PatientUserId,

                DepartmentId = request.DepartmentId,

                RequestedServiceId = request.RequestedServiceId,

                RequestedDentistUserId = request.RequestedDentistUserId,

                AssignedDentistUserId = shouldConfirm ? assignedDentistId : null,

                RoomId = shouldConfirm ? roomId : null,

                DentalChairId = shouldConfirm ? dentalChairId : null,

                ScheduledStart = requestedStart,

                ScheduledEnd = scheduledEnd,

                ReasonForVisit = string.IsNullOrWhiteSpace(request.ReasonForVisit) ? "Khám bệnh tại quầy" : request.ReasonForVisit.Trim(),

                BookingSource = BookingSource.Receptionist,

                QueueNumber = shouldConfirm ? queueNumber : null,

                Status = shouldConfirm ? AppointmentStatus.Confirmed : AppointmentStatus.PendingReceptionReview,

                ConfirmedAt = shouldConfirm ? now : null,

                CreatedByUserId = receptionistUserId,

                CreatedAt = now

            };

            _dbContext.Appointments.Add(appointment);

            var history = new AppointmentStatusHistory

            {

                Appointment = appointment,

                OldStatus = null,

                NewStatus = appointment.Status,

                ChangedByUserId = receptionistUserId,

                Note = shouldConfirm

                    ? $"Lễ tân tiếp nhận, phân bổ tài nguyên và xác nhận lịch hẹn tại quầy. Số thứ tự: {queueNumber}."

                    : "Lễ tân tiếp nhận và tạo yêu cầu đặt lịch hẹn tại quầy.",

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // P2: Đồng bộ trạng thái FollowUpSchedule khi Appointment được xác nhận

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && (linkedFollowUp.Status == "Pending" || linkedFollowUp.Status == "Scheduled" || linkedFollowUp.Status == "Proposed"))

            {

                linkedFollowUp.Status = "Scheduled";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)

            {

                await tx.CommitAsync(ct);

                committed = true;

            }

            var dto = await MapToDtoAsync(appointment.AppointmentId, ct);

            return ApiResponse<AppointmentDto>.Ok(dto!, shouldConfirm

                ? $"Tiếp nhận, phân bổ tài nguyên và xác nhận lịch hẹn tại quầy thành công (STT: {queueNumber})."

                : "Tiếp nhận và tạo lịch hẹn tại quầy thành công.");

        }

        catch

        {

            if (tx != null && !committed)

            {

                await tx.RollbackAsync(ct);

            }

            throw;

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null)

            {

                await tx.DisposeAsync();

            }

        }

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

            .Include(a => a.Department)

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy thông tin lịch hẹn.");

        }

        // Authorization checks

        if (currentRole == UserRole.Patient && appointment.PatientUserId != currentUserId)

        {

            return ApiResponse<AppointmentDto>.Fail("Bạn không có quyền truy cập lịch hẹn này.");

        }

        if (currentRole == UserRole.Dentist && appointment.AssignedDentistUserId != currentUserId)

        {

            return ApiResponse<AppointmentDto>.Fail("Lịch hẹn này không được phân công cho bạn.");

        }

        if (currentRole == UserRole.DepartmentManager && appointment.Department?.ManagerUserId != currentUserId)

        {

            // Allow if manager manages this department

            return ApiResponse<AppointmentDto>.Fail("Bạn không quản lý chuyên khoa của lịch hẹn này.");

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

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        IDisposable? lockScope = null;

        try

        {

            var lockKeys = new[] { SqlAppLockHelper.ApptKey(appointmentId) };

            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            var appointment = await _dbContext.Appointments

                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

            }

            if (isRelational) await _dbContext.Entry(appointment).ReloadAsync(ct);

            if (appointment.PatientUserId != patientUserId)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Bạn không có quyền rút yêu cầu đặt lịch này.");

            }

            var withdrawableStatuses = new[]

            {

                AppointmentStatus.PendingReceptionReview,

                AppointmentStatus.PendingDepartmentReview,

                AppointmentStatus.AwaitingPatientResponse

            };

            if (!withdrawableStatuses.Contains(appointment.Status))

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail($"Không thể rút yêu cầu khi lịch hẹn đang ở trạng thái {appointment.Status}.");

            }

            var oldStatus = appointment.Status;

            var now = DateTime.UtcNow;

            appointment.Status = AppointmentStatus.Withdrawn;

            appointment.CancelledAt = now;

            appointment.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Bệnh nhân rút lại yêu cầu đặt lịch." : reason.Trim();

            appointment.UpdatedAt = now;

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.Withdrawn,

                ChangedByUserId = patientUserId,

                Note = appointment.CancellationReason,

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // Expire any pending change proposals

            var pendingProposals = await _dbContext.AppointmentChangeProposals

                .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

                .ToListAsync(ct);

            foreach (var p in pendingProposals)

            {

                p.Status = ProposalStatus.Expired;

                p.RespondedAt = now;

                p.Reason += " [Rút lịch hẹn gốc]";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null) await tx.CommitAsync(ct);

            return ApiResponse.Ok("Đã rút yêu cầu đặt lịch khám thành công.");

        }

        catch

        {

            if (tx != null) await tx.RollbackAsync(ct);

            throw;

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

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

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        IDisposable? lockScope = null;

        try

        {

            var lockKeys = new[] { SqlAppLockHelper.ApptKey(appointmentId) };

            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            var appointment = await _dbContext.Appointments

                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

            }

            if (isRelational) await _dbContext.Entry(appointment).ReloadAsync(ct);

            if (appointment.PatientUserId != patientUserId)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Bạn không có quyền hủy lịch hẹn này.");

            }

            var withdrawableStatuses = new[]

            {

                AppointmentStatus.PendingReceptionReview,

                AppointmentStatus.PendingDepartmentReview,

                AppointmentStatus.AwaitingPatientResponse

            };

            var now = DateTime.UtcNow;

            if (withdrawableStatuses.Contains(appointment.Status))

            {

                var oldStatusW = appointment.Status;

                appointment.Status = AppointmentStatus.Withdrawn;

                appointment.CancelledAt = now;

                appointment.CancellationReason = reason.Trim();

                appointment.UpdatedAt = now;

                var historyW = new AppointmentStatusHistory

                {

                    AppointmentId = appointmentId,

                    OldStatus = oldStatusW,

                    NewStatus = AppointmentStatus.Withdrawn,

                    ChangedByUserId = patientUserId,

                    Note = appointment.CancellationReason,

                    ChangedAt = now

                };

                _dbContext.AppointmentStatusHistories.Add(historyW);

                var pendingProposalsW = await _dbContext.AppointmentChangeProposals

                    .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

                    .ToListAsync(ct);

                foreach (var p in pendingProposalsW)

                {

                    p.Status = ProposalStatus.Expired;

                    p.RespondedAt = now;

                    p.Reason += " [Rút lịch hẹn gốc]";

                }

                await _dbContext.SaveChangesAsync(ct);

                if (tx != null) await tx.CommitAsync(ct);

                return ApiResponse.Ok("Đã rút yêu cầu đặt lịch khám thành công.");

            }

            if (appointment.Status != AppointmentStatus.Confirmed)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.Withdrawn)

                {

                    return ApiResponse.Fail("Lịch hẹn này đã được hủy trước đó.");

                }

                if (appointment.Status == AppointmentStatus.Completed)

                {

                    return ApiResponse.Fail("Không thể hủy lịch hẹn đã hoàn thành.");

                }

                return ApiResponse.Fail($"Chỉ có thể hủy lịch hẹn đang chờ duyệt hoặc đã được xác nhận (Hiện tại: {appointment.Status}).");

            }

            var localNow = ClinicTimeHelper.Now;

            bool isLate = false;

            double hoursNotice = 0;

            if (appointment.ScheduledStart.HasValue)

            {

                hoursNotice = (appointment.ScheduledStart.Value - localNow).TotalHours;

                isLate = hoursNotice < 12;

            }

            var oldStatus = appointment.Status;

            appointment.Status = AppointmentStatus.Cancelled;

            appointment.CancelledAt = now;

            var reasonPrefix = isLate ? "[Hủy sát giờ <12h] " : "";

            appointment.CancellationReason = $"{reasonPrefix}{reason.Trim()}";

            appointment.UpdatedAt = now;

            var historyNote = isLate

                ? $"Bệnh nhân hủy lịch sát giờ (<12h, báo trước {Math.Max(0, Math.Round(hoursNotice, 1))}h): {reason.Trim()}"

                : $"Bệnh nhân hủy lịch: {reason.Trim()}";

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.Cancelled,

                ChangedByUserId = patientUserId,

                Note = historyNote,

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // Also expire any pending proposals on this appointment so they cannot be approved later

            var pendingProposals = await _dbContext.AppointmentChangeProposals

                .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

                .ToListAsync(ct);

            foreach (var p in pendingProposals)

            {

                p.Status = ProposalStatus.Expired;

                p.RespondedAt = now;

                p.Reason += " [Hủy tự động do cuộc hẹn gốc đã bị hủy]";

            }

            // P2: Đồng bộ trạng thái FollowUpSchedule nếu có liên kết

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && linkedFollowUp.Status != "Completed")

            {

                linkedFollowUp.Status = "Cancelled";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null) await tx.CommitAsync(ct);

            // Fetch updated strike eligibility & send notification outside transaction

            try

            {

                var eligibilityResult = await GetBookingEligibilityAsync(patientUserId, ct);

                var strikeCount = eligibilityResult.Data?.StrikeCount ?? (isLate ? 1 : 0);

                var isBlocked = eligibilityResult.Data?.IsEligible == false;

                var blockedUntil = eligibilityResult.Data?.BlockedUntil;

                if (isLate)

                {

                    if (isBlocked && blockedUntil.HasValue)

                    {

                        await _notificationService.CreateNotificationAsync(

                            patientUserId,

                            appointmentId,

                            "AppointmentStrikePolicyBlocked",

                            "Cảnh báo vi phạm & Tạm khóa đặt lịch online",

                            $"Bạn vừa hủy lịch hẹn {appointment.AppointmentCode} sát giờ (<12h). Tài khoản đã tích lũy {strikeCount} lần vi phạm trong 30 ngày qua. Quyền tự đặt lịch trực tuyến tạm khóa đến {blockedUntil.Value:dd/MM/yyyy HH:mm}. Vui lòng liên hệ hotline 1900 1234 nếu cần hỗ trợ.",

                            ct);

                    }

                    else

                    {

                        await _notificationService.CreateNotificationAsync(

                            patientUserId,

                            appointmentId,

                            "AppointmentStrikeWarning",

                            "Cảnh báo hủy lịch hẹn sát giờ (<12h)",

                            $"Bạn vừa hủy lịch hẹn {appointment.AppointmentCode} sát giờ (<12h) và bị tính 1 gậy vi phạm. Hiện tại bạn có {strikeCount}/3 lần vi phạm trong 30 ngày qua. Nếu đạt 3 lần, quyền tự đặt lịch trực tuyến sẽ bị tạm khóa 30 ngày.",

                            ct);

                    }

                }

                else

                {

                    await _notificationService.CreateNotificationAsync(

                        patientUserId,

                        appointmentId,

                        "AppointmentCancelled",

                        "Lịch hẹn đã bị hủy",

                        $"Lịch hẹn {appointment.AppointmentCode} đã được hủy thành công theo yêu cầu của bạn.",

                        ct);

                }

                if (_reminderService != null)

                {

                    await _reminderService.CancelRemindersForAppointmentAsync(appointmentId, ct);

                }

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Lỗi khi gửi thông báo hủy lịch cho cuộc hẹn {AppointmentId}.", appointmentId);

            }

            var successMessage = isLate

                ? "Hủy lịch hẹn thành công. Lưu ý: Lịch hẹn bị hủy sát giờ (<12 tiếng) nên hệ thống đã ghi nhận 1 lần vi phạm."

                : "Hủy lịch hẹn thành công.";

            return ApiResponse.Ok(successMessage);

        }

        catch

        {

            if (tx != null) await tx.RollbackAsync(ct);

            throw;

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

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

        var clinicNow = ClinicTimeHelper.Now;

        // Validate new schedule time

        if (newStart <= clinicNow)

        {

            return ApiResponse<AppointmentDto>.Fail("Thời gian khám mới không được nằm trong quá khứ.");

        }

        // Validate booking eligibility

        var eligibilityResult = await GetBookingEligibilityAsync(patientUserId, ct);

        if (eligibilityResult.Data?.IsEligible == false)

        {

            return ApiResponse<AppointmentDto>.Fail(eligibilityResult.Data.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt lịch khám trực tuyến.");

        }

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        IDisposable? lockScope = null;

        bool committed = false;

        Appointment? appointment = null;

        Appointment? newAppointment = null;

        string? newCode = null;

        try

        {

            var lockKeys = new[] { SqlAppLockHelper.ApptKey(appointmentId) };

            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            appointment = await _dbContext.Appointments

                .Include(a => a.RequestedService)

                .Include(a => a.Department)

                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse<AppointmentDto>.Fail("Không tìm thấy lịch hẹn.");

            }

            if (isRelational) await _dbContext.Entry(appointment).ReloadAsync(ct);

            if (appointment.PatientUserId != patientUserId)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse<AppointmentDto>.Fail("Bạn không có quyền dời lịch hẹn này.");

            }

            var reschedulableStatuses = new[]

            {

                AppointmentStatus.PendingReceptionReview,

                AppointmentStatus.PendingDepartmentReview,

                AppointmentStatus.AwaitingPatientResponse,

                AppointmentStatus.Confirmed

            };

            if (!reschedulableStatuses.Contains(appointment.Status))

            {

                if (tx != null) await tx.RollbackAsync(ct);

                if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.Withdrawn)

                {

                    return ApiResponse<AppointmentDto>.Fail("Lịch hẹn này đã bị hủy trước đó, không thể dời lịch.");

                }

                if (appointment.Status == AppointmentStatus.Completed)

                {

                    return ApiResponse<AppointmentDto>.Fail("Lịch hẹn này đã hoàn thành, không thể dời lịch.");

                }

                return ApiResponse<AppointmentDto>.Fail($"Không thể dời lịch khi ca khám đang ở trạng thái {appointment.Status}.");

            }

            // Validate requested dentist if changed

            if (request.NewRequestedDentistUserId.HasValue && request.NewRequestedDentistUserId.Value > 0)

            {

                var isDentistInDept = await _dbContext.DentistDepartments

                    .AsNoTracking()

                    .AnyAsync(dd => dd.DentistUserId == request.NewRequestedDentistUserId.Value &&

                                    dd.DepartmentId == appointment.DepartmentId &&

                                    dd.Status == "Active", ct);

                if (!isDentistInDept)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail("Bác sĩ được yêu cầu không thuộc chuyên khoa của ca khám này.");

                }

            }

            if (appointment.RequestedService != null && appointment.RequestedService.MinLeadTimeHours > 0)

            {

                if (newStart < clinicNow.AddHours(appointment.RequestedService.MinLeadTimeHours).AddMinutes(-1))

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse<AppointmentDto>.Fail($"Dịch vụ '{appointment.RequestedService.ServiceName}' yêu cầu đặt lịch trước tối thiểu {appointment.RequestedService.MinLeadTimeHours} giờ.");

                }

            }

            var duration = appointment.RequestedService?.DurationMinutes ?? 30;

            if (duration <= 0) duration = 30;

            var scheduledEnd = newStart.AddMinutes(duration);

            newCode = await GenerateUniqueAppointmentCodeAsync(ct);

            var now = DateTime.UtcNow;

            // 1. Cancel old appointment and free resources

            var oldStatus = appointment.Status;

            appointment.RoomId = null;

            appointment.DentalChairId = null;

            appointment.AssignedDentistUserId = null;

            appointment.Status = AppointmentStatus.Cancelled;

            appointment.CancelledAt = now;

            appointment.CancellationReason = $"[Dời lịch] Hủy để dời sang lịch khám mới {newCode}. Lý do: {request.Reason.Trim()}";

            appointment.UpdatedAt = now;

            var oldHistory = new AppointmentStatusHistory

            {

                AppointmentId = appointment.AppointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.Cancelled,

                ChangedByUserId = patientUserId,

                Note = $"Bệnh nhân hủy hẹn để dời sang lịch mới: {newCode}. Lý do: {request.Reason.Trim()}",

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(oldHistory);

            // Expire any pending change proposals for the old appointment

            var pendingProposals = await _dbContext.AppointmentChangeProposals

                .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

                .ToListAsync(ct);

            foreach (var p in pendingProposals)

            {

                p.Status = ProposalStatus.Expired;

                p.RespondedAt = now;

                p.Reason = (string.IsNullOrWhiteSpace(p.Reason) ? "" : p.Reason + " ") + "[Dời sang lịch hẹn mới]";

            }

            if (_reminderService != null)

            {

                try

                {

                    await _reminderService.CancelRemindersForAppointmentAsync(appointment.AppointmentId, ct);

                }

                catch (Exception remEx)

                {

                    _logger.LogWarning(remEx, "Failed to cancel reminders for rescheduled appointment {AppointmentId}", appointment.AppointmentId);

                }

            }

            // 2. Create new appointment

            newAppointment = new Appointment

            {

                AppointmentCode = newCode,

                PatientUserId = patientUserId,

                DepartmentId = appointment.DepartmentId,

                RequestedServiceId = appointment.RequestedServiceId,

                RequestedDentistUserId = request.NewRequestedDentistUserId ?? appointment.RequestedDentistUserId,

                AssignedDentistUserId = null,

                RoomId = null,

                DentalChairId = null,

                ScheduledStart = newStart,

                ScheduledEnd = scheduledEnd,

                ReasonForVisit = $"{appointment.ReasonForVisit} (Dời từ lịch {appointment.AppointmentCode})",

                BookingSource = BookingSource.Patient,

                QueueNumber = null,

                Status = AppointmentStatus.PendingReceptionReview,

                CreatedByUserId = patientUserId,

                CreatedAt = now

            };

            _dbContext.Appointments.Add(newAppointment);

            var newHistory = new AppointmentStatusHistory

            {

                Appointment = newAppointment,

                OldStatus = null,

                NewStatus = AppointmentStatus.PendingReceptionReview,

                ChangedByUserId = patientUserId,

                Note = $"Đặt lịch mới chuyển từ lịch đã hủy {appointment.AppointmentCode}. Lý do: {request.Reason.Trim()}",

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(newHistory);

            // P2: Chuyển liên kết FollowUpSchedule sang Appointment mới và giữ trạng thái phù hợp

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null)

            {

                linkedFollowUp.Appointment = newAppointment;

                linkedFollowUp.Status = "Scheduled";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)

            {

                await tx.CommitAsync(ct);

                committed = true;

            }

        }

        catch

        {

            if (tx != null && !committed)

            {

                await tx.RollbackAsync(ct);

            }

            throw;

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

        // Post-commit side effect: send notification (does not rollback or fail the main operation if delivery fails)

        try

        {

            await _notificationService.CreateNotificationAsync(

                patientUserId,

                newAppointment!.AppointmentId,

                "AppointmentRescheduled",

                "Dời lịch khám thành công",

                $"Lịch hẹn {appointment!.AppointmentCode} đã được hủy và chuyển sang lịch hẹn mới {newCode} vào lúc {request.NewScheduledStart:dd/MM/yyyy HH:mm}. Vui lòng chờ Lễ tân tiếp nhận.",

                ct);

        }

        catch (Exception notifEx)

        {

            _logger.LogWarning(notifEx, "Failed to send notification for rescheduled appointment {AppointmentId}", appointment?.AppointmentId);

        }

        var dto = await MapToDtoAsync(newAppointment!.AppointmentId, ct);

        return ApiResponse<AppointmentDto>.Ok(dto!, $"Đã hủy lịch hẹn cũ ({appointment!.AppointmentCode}) và dời sang lịch hẹn mới ({newCode}) vào lúc {request.NewScheduledStart:dd/MM/yyyy HH:mm} thành công!");

    }

    public async Task<ApiResponse<AppointmentChangeProposalDto>> GetAppointmentProposalAsync(

        long appointmentId,

        long patientUserId,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .AsNoTracking()

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)

        {

            return ApiResponse<AppointmentChangeProposalDto>.Fail("Không tìm thấy đề xuất thay đổi lịch hẹn.");

        }

        var proposal = await _dbContext.AppointmentChangeProposals

            .AsNoTracking()

            .Include(p => p.ProposedByUser)

                .ThenInclude(u => u.StaffProfile)

            .Include(p => p.ProposedDepartment)

            .Include(p => p.ProposedDentistUser)

                .ThenInclude(d => d!.User)

            .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

            .OrderByDescending(p => p.CreatedAt)

            .Select(p => new AppointmentChangeProposalDto

            {

                ProposalId = p.ProposalId,

                AppointmentId = p.AppointmentId,

                ProposedByUserId = p.ProposedByUserId,

                ProposedByName = p.ProposedByUser.StaffProfile != null ? p.ProposedByUser.StaffProfile.FullName : null,

                ProposedDepartmentId = p.ProposedDepartmentId,

                ProposedDepartmentName = p.ProposedDepartment != null ? p.ProposedDepartment.Name : null,

                ProposedDentistUserId = p.ProposedDentistUserId,

                ProposedDentistName = p.ProposedDentistUser != null ? p.ProposedDentistUser.User.FullName : null,

                ProposedStart = p.ProposedStart,

                ProposedEnd = p.ProposedEnd,

                Reason = p.Reason,

                Status = p.Status,

                CreatedAt = p.CreatedAt,

                RespondedAt = p.RespondedAt

            })

            .FirstOrDefaultAsync(ct);

        if (proposal == null)

        {

            return ApiResponse<AppointmentChangeProposalDto>.Fail("Không có đề xuất thay đổi nào đang chờ phản hồi.");

        }

        return ApiResponse<AppointmentChangeProposalDto>.Ok(proposal);

    }

    public async Task<ApiResponse> PatientRespondProposalAsync(

        long appointmentId,

        long patientUserId,

        bool accept,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)

        {

            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");

        }

        if (appointment.Status != AppointmentStatus.AwaitingPatientResponse)

        {

            return ApiResponse.Fail("Lịch hẹn hiện không ở trạng thái chờ bệnh nhân phản hồi.");

        }

        var proposal = await _dbContext.AppointmentChangeProposals

            .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

            .OrderByDescending(p => p.CreatedAt)

            .FirstOrDefaultAsync(ct);

        if (proposal == null)

        {

            return ApiResponse.Fail("Không tìm thấy đề xuất thay đổi nào đang chờ.");

        }

        var now = DateTime.UtcNow;

        if (accept)

        {

            // Revalidate availability of proposed dentist & slot

            var targetDentist = proposal.ProposedDentistUserId ?? appointment.RequestedDentistUserId;

            var targetStart = proposal.ProposedStart ?? appointment.ScheduledStart;

            var targetEnd = proposal.ProposedEnd ?? appointment.ScheduledEnd;

            if (targetDentist.HasValue && targetStart.HasValue && targetEnd.HasValue)

            {

                var occupiedStatuses = new[]

                {

                    AppointmentStatus.Confirmed,

                    AppointmentStatus.CheckedIn,

                    AppointmentStatus.Waiting,

                    AppointmentStatus.Called,

                    AppointmentStatus.InService

                };

                bool conflict = await _dbContext.Appointments

                    .AnyAsync(a => a.AppointmentId != appointmentId &&

                                   a.AssignedDentistUserId == targetDentist.Value &&

                                   occupiedStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < targetEnd.Value &&

                                   a.ScheduledEnd > targetStart.Value, ct);

                if (conflict)

                {

                    return ApiResponse.Fail("Khung giờ hoặc bác sĩ được đề xuất không còn khả dụng do đã có lịch khác. Trưởng khoa sẽ sắp xếp lại.");

                }

            }

            // Apply proposed values

            if (proposal.ProposedDepartmentId.HasValue)

                appointment.DepartmentId = proposal.ProposedDepartmentId.Value;

            if (proposal.ProposedDentistUserId.HasValue)

                appointment.RequestedDentistUserId = proposal.ProposedDentistUserId.Value;

            if (proposal.ProposedStart.HasValue)

                appointment.ScheduledStart = proposal.ProposedStart.Value;

            if (proposal.ProposedEnd.HasValue)

                appointment.ScheduledEnd = proposal.ProposedEnd.Value;

            proposal.Status = ProposalStatus.Accepted;

            proposal.RespondedAt = now;

            var oldStatus = appointment.Status;

            appointment.Status = AppointmentStatus.PendingReceptionReview;

            appointment.UpdatedAt = now;

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.PendingReceptionReview,

                ChangedByUserId = patientUserId,

                Note = "Bệnh nhân đồng ý với đề xuất thay đổi. Chờ Lễ tân xác nhận.",

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // P2: Đồng bộ trạng thái FollowUpSchedule khi Appointment được xác nhận

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && (linkedFollowUp.Status == "Pending" || linkedFollowUp.Status == "Scheduled" || linkedFollowUp.Status == "Proposed"))

            {

                linkedFollowUp.Status = "Scheduled";

            }

            await _dbContext.SaveChangesAsync(ct);

            await _notificationService.CreateNotificationAsync(

                patientUserId,

                appointmentId,

                "ProposalAccepted",

                "Đã chấp nhận đề xuất",

                $"Bạn đã đồng ý với đề xuất thay đổi lịch hẹn {appointment.AppointmentCode}. Trưởng khoa sẽ tiến hành xác nhận chính thức.",

                ct);

            return ApiResponse.Ok("Bạn đã chấp nhận đề xuất thay đổi lịch hẹn.");

        }

        else

        {

            proposal.Status = ProposalStatus.Rejected;

            proposal.RespondedAt = now;

            var oldStatus = appointment.Status;

            appointment.Status = AppointmentStatus.PendingReceptionReview;

            appointment.UpdatedAt = now;

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.PendingReceptionReview,

                ChangedByUserId = patientUserId,

                Note = "Bệnh nhân từ chối đề xuất thay đổi. Chuyển lại Lễ tân xem xét phương án khác.",

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // P2: Đồng bộ trạng thái FollowUpSchedule khi Appointment được xác nhận

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && (linkedFollowUp.Status == "Pending" || linkedFollowUp.Status == "Scheduled" || linkedFollowUp.Status == "Proposed"))

            {

                linkedFollowUp.Status = "Scheduled";

            }

            await _dbContext.SaveChangesAsync(ct);

            return ApiResponse.Ok("Bạn đã từ chối đề xuất thay đổi. Yêu cầu được chuyển lại cho Trưởng khoa để xem xét phương án khác.");

        }

    }

    public async Task<ApiResponse<BookingEligibilityDto>> GetBookingEligibilityAsync(

        long patientUserId,

        CancellationToken ct = default)

    {

        var now = DateTime.UtcNow;

        var thirtyDaysAgo = now.AddDays(-30);

        var candidateAppointments = await _dbContext.Appointments

            .AsNoTracking()

            .Where(a => a.PatientUserId == patientUserId &&

                        (a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow))

            .OrderByDescending(a => a.CancelledAt ?? a.ScheduledStart ?? a.CreatedAt)

            .ToListAsync(ct);

        var strikes = new List<StrikeIncidentDto>();

        foreach (var appt in candidateAppointments)

        {

            if (appt.Status == AppointmentStatus.Cancelled)

            {

                var incidentDate = appt.CancelledAt ?? appt.UpdatedAt ?? appt.CreatedAt;

                if (incidentDate < thirtyDaysAgo) continue;

                bool isLate = false;

                // Staff or clinic cancellations are never penalized against the patient

                if (appt.CancellationReason != null && 

                    (appt.CancellationReason.Contains("[Lễ tân") || 

                     appt.CancellationReason.Contains("[Phòng khám") || 

                     appt.CancellationReason.Contains("[Staff]") ||

                     appt.CancellationReason.Contains("[Clinic]") ||

                     appt.CancellationReason.Contains("[Dời lịch]")))

                {

                    isLate = false;

                }

                else if (appt.CancellationReason != null && appt.CancellationReason.Contains("[Hủy sát giờ <12h]"))

                {

                    isLate = true;

                }

                else if (appt.ScheduledStart.HasValue && appt.CancelledAt.HasValue)

                {

                    var cancelLocal = appt.CancelledAt.Value.Kind == DateTimeKind.Utc 

                        ? appt.CancelledAt.Value.ToLocalTime() 

                        : appt.CancelledAt.Value;

                    var hoursNotice = (appt.ScheduledStart.Value - cancelLocal).TotalHours;

                    if (hoursNotice < 12)

                    {

                        isLate = true;

                    }

                }

                if (isLate)

                {

                    strikes.Add(new StrikeIncidentDto

                    {

                        AppointmentId = appt.AppointmentId,

                        AppointmentCode = appt.AppointmentCode,

                        Type = "LateCancellation",

                        Description = $"Hủy lịch hẹn sát giờ (<12h): {appt.CancellationReason ?? "Không có lý do"}",

                        IncidentDate = incidentDate

                    });

                }

            }

            else if (appt.Status == AppointmentStatus.NoShow)

            {

                var incidentDate = appt.ScheduledStart ?? appt.UpdatedAt ?? appt.CreatedAt;

                if (incidentDate >= thirtyDaysAgo)

                {

                    strikes.Add(new StrikeIncidentDto

                    {

                        AppointmentId = appt.AppointmentId,

                        AppointmentCode = appt.AppointmentCode,

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

                result.BlockReason = $"Tài khoản đã tích lũy {strikes.Count} lần vi phạm hủy lịch/vắng mặt trong 30 ngày qua. Quyền tự đặt lịch trực tuyến tạm khóa đến {blockedUntil:dd/MM/yyyy HH:mm}. Vui lòng liên hệ hotline 1900 1234 để được hỗ trợ.";

            }

            else

            {

                result.IsEligible = true;

            }

        }

        else

        {

            result.IsEligible = true;

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

        string? note,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

        }

        if (appointment.Status != AppointmentStatus.PendingReceptionReview)

        {

            return ApiResponse.Fail($"Không thể chuyển tiếp khi lịch hẹn đang ở trạng thái {appointment.Status}.");

        }

        var oldStatus = appointment.Status;

        appointment.Status = AppointmentStatus.PendingDepartmentReview;

        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory

        {

            AppointmentId = appointmentId,

            OldStatus = oldStatus,

            NewStatus = AppointmentStatus.PendingDepartmentReview,

            ChangedByUserId = staffUserId,

            Note = string.IsNullOrWhiteSpace(note) ? "Lễ tân tiếp nhận và chuyển tiếp sang Trưởng khoa xem xét." : note.Trim(),

            ChangedAt = DateTime.UtcNow

        };

        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse.Ok("Đã chuyển tiếp yêu cầu sang Trưởng khoa thành công.");

    }

    public async Task<ApiResponse> ReceptionistRejectAsync(

        long appointmentId,

        long staffUserId,

        string reason,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

        }

        if (appointment.Status != AppointmentStatus.PendingReceptionReview)

        {

            return ApiResponse.Fail($"Không thể từ chối yêu cầu ở trạng thái {appointment.Status}.");

        }

        if (string.IsNullOrWhiteSpace(reason))

        {

            return ApiResponse.Fail("Vui lòng nhập lý do từ chối yêu cầu đặt lịch.");

        }

        var oldStatus = appointment.Status;

        appointment.Status = AppointmentStatus.Rejected;

        appointment.CancelledAt = DateTime.UtcNow;

        appointment.CancellationReason = reason.Trim();

        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory

        {

            AppointmentId = appointmentId,

            OldStatus = oldStatus,

            NewStatus = AppointmentStatus.Rejected,

            ChangedByUserId = staffUserId,

            Note = $"Lễ tân từ chối yêu cầu: {reason.Trim()}",

            ChangedAt = DateTime.UtcNow

        };

        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(

            appointment.PatientUserId,

            appointmentId,

            "AppointmentRejected",

            "Yêu cầu đặt lịch đã bị từ chối",

            $"Yêu cầu đặt lịch {appointment.AppointmentCode} đã bị từ chối bởi lễ tân. Lý do: {reason.Trim()}",

            ct);

        return ApiResponse.Ok("Đã từ chối yêu cầu đặt lịch.");

    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetReceptionistTodayConfirmedAsync(
        string? searchQuery = null,
        CancellationToken ct = default)
    {
        var today = ClinicTimeHelper.Today.Date;
        var tomorrow = today.AddDays(1);

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart != null &&
                        a.ScheduledStart.Value >= today &&
                        a.ScheduledStart.Value < tomorrow &&
                        (a.Status == AppointmentStatus.Confirmed ||
                         a.Status == AppointmentStatus.CheckedIn ||
                         a.Status == AppointmentStatus.Waiting ||
                         a.Status == AppointmentStatus.InService ||
                         a.Status == AppointmentStatus.Completed ||
                         a.Status == AppointmentStatus.NoShow));

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim().ToLower();
            query = query.Where(a =>
                a.AppointmentCode.ToLower().Contains(q) ||
                a.PatientUser.PatientCode.ToLower().Contains(q) ||
                a.PatientUser.FullName.ToLower().Contains(q) ||
                (a.PatientUser.User.PhoneNumber != null && a.PatientUser.User.PhoneNumber.Contains(q)));
        }

        var finalQuery = query
            .OrderBy(a => a.QueueNumber)
            .ThenBy(a => a.ScheduledStart);

        var list = await MapToDtoListAsync(finalQuery, ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse> ReceptionistCheckInAsync(

        long appointmentId,

        long staffUserId,

        string? note,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

        }

        if (appointment.Status != AppointmentStatus.Confirmed)

        {

            return ApiResponse.Fail($"Chỉ có thể check-in cho lịch hẹn ở trạng thái Confirmed (Hiện tại: {appointment.Status}).");

        }

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        var targetDate = (appointment.ScheduledStart ?? ClinicTimeHelper.Today).Date;

        IDisposable? lockScope = null;

        bool committed = false;

        try

        {

            if (isRelational && appointment.DepartmentId.HasValue)

            {

                var lockKeys = new List<string>

                {

                    SqlAppLockHelper.ApptKey(appointmentId),

                    SqlAppLockHelper.DeptQueueKey(appointment.DepartmentId.Value, targetDate)

                };

                lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            }

            if (string.IsNullOrWhiteSpace(appointment.QueueNumber))

            {

                var existingQueueNumbers = await _dbContext.Appointments

                    .Where(a => a.DepartmentId == appointment.DepartmentId &&

                                a.ScheduledStart != null &&

                                a.ScheduledStart.Value.Date == targetDate &&

                                a.QueueNumber != null &&

                                a.QueueNumber != "")

                    .Select(a => a.QueueNumber!)

                    .ToListAsync(ct);

                int maxSeq = 0;

                foreach (var qn in existingQueueNumbers)

                {

                    if (!string.IsNullOrEmpty(qn) && qn.StartsWith("Q") && int.TryParse(qn.Substring(1), out int num))

                    {

                        if (num > maxSeq) maxSeq = num;

                    }

                }

                appointment.QueueNumber = $"Q{(maxSeq + 1):D3}";

            }

            var oldStatus = appointment.Status;

            appointment.Status = AppointmentStatus.CheckedIn;

            appointment.CheckedInAt = DateTime.UtcNow;

            appointment.UpdatedAt = DateTime.UtcNow;

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.CheckedIn,

                ChangedByUserId = staffUserId,

                Note = string.IsNullOrWhiteSpace(note)

                    ? $"Bệnh nhân đã đến và hoàn tất thủ tục Check-in. Số thứ tự: {appointment.QueueNumber}."

                    : $"{note.Trim()} (Số thứ tự: {appointment.QueueNumber})",

                ChangedAt = DateTime.UtcNow

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // P2: Đồng bộ trạng thái FollowUpSchedule khi Appointment được xác nhận

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && (linkedFollowUp.Status == "Pending" || linkedFollowUp.Status == "Scheduled" || linkedFollowUp.Status == "Proposed"))

            {

                linkedFollowUp.Status = "Scheduled";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)

            {

                await tx.CommitAsync(ct);

                committed = true;

            }

            return ApiResponse.Ok($"Check-in bệnh nhân thành công. Số thứ tự: {appointment.QueueNumber}.");

        }

        catch

        {

            if (tx != null && !committed) await tx.RollbackAsync(ct);

            throw;

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

    }

    public async Task<ApiResponse> ReceptionistMarkNoShowAsync(

        long appointmentId,

        long staffUserId,

        string? note = null,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

        }

        if (appointment.Status != AppointmentStatus.Confirmed &&

            appointment.Status != AppointmentStatus.Waiting)

        {

            return ApiResponse.Fail($"Chỉ có thể đánh dấu vắng mặt cho lịch hẹn ở trạng thái Confirmed hoặc Waiting (Hiện tại: {appointment.Status}).");

        }

        var oldStatus = appointment.Status;

        var now = DateTime.UtcNow;

        appointment.Status = AppointmentStatus.NoShow;

        appointment.UpdatedAt = now;

        var historyNote = string.IsNullOrWhiteSpace(note)

            ? "Lễ tân đánh dấu bệnh nhân không đến khám (No-Show)."

            : $"Lễ tân đánh dấu vắng mặt (No-Show): {note.Trim()}";

        var history = new AppointmentStatusHistory

        {

            AppointmentId = appointmentId,

            OldStatus = oldStatus,

            NewStatus = AppointmentStatus.NoShow,

            ChangedByUserId = staffUserId,

            Note = historyNote,

            ChangedAt = now

        };

        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        var eligibilityResult = await GetBookingEligibilityAsync(appointment.PatientUserId, ct);

        var strikeCount = eligibilityResult.Data?.StrikeCount ?? 1;

        var isBlocked = eligibilityResult.Data?.IsEligible == false;

        var blockedUntil = eligibilityResult.Data?.BlockedUntil;

        if (isBlocked && blockedUntil.HasValue)

        {

            await _notificationService.CreateNotificationAsync(

                appointment.PatientUserId,

                appointmentId,

                "AppointmentNoShowBlocked",

                "Ghi nhận vắng mặt & Tạm khóa đặt lịch online",

                $"Bạn đã không đến khám lịch hẹn {appointment.AppointmentCode} (No-Show). Tài khoản đã tích lũy {strikeCount} lần vi phạm trong 30 ngày qua. Quyền tự đặt lịch trực tuyến tạm khóa đến {blockedUntil.Value:dd/MM/yyyy HH:mm}. Vui lòng liên hệ hotline 1900 1234 để được hỗ trợ.",

                ct);

        }

        else

        {

            await _notificationService.CreateNotificationAsync(

                appointment.PatientUserId,

                appointmentId,

                "AppointmentNoShowWarning",

                "Ghi nhận vắng mặt buổi khám (No-Show)",

                $"Bạn đã không đến khám lịch hẹn {appointment.AppointmentCode} đúng hẹn và bị tính 1 gậy vi phạm. Hiện bạn có {strikeCount}/3 lần vi phạm trong 30 ngày qua. Nếu đạt 3 lần, quyền tự đặt lịch trực tuyến sẽ bị tạm khóa 30 ngày.",

                ct);

        }

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

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        IDisposable? lockScope = null;

        bool committed = false;

        Appointment? appointment = null;

        try

        {

            var lockKeys = new[] { SqlAppLockHelper.ApptKey(appointmentId) };

            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            appointment = await _dbContext.Appointments

                .Include(a => a.PatientUser)

                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

            }

            if (isRelational) await _dbContext.Entry(appointment).ReloadAsync(ct);

            var cancellableStatuses = new[]

            {

                AppointmentStatus.PendingReceptionReview,

                AppointmentStatus.PendingDepartmentReview,

                AppointmentStatus.AwaitingPatientResponse,

                AppointmentStatus.Confirmed,

                AppointmentStatus.Waiting

            };

            if (!cancellableStatuses.Contains(appointment.Status))

            {

                if (tx != null) await tx.RollbackAsync(ct);

                if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.Withdrawn)

                {

                    return ApiResponse.Fail("Lịch hẹn này đã được hủy trước đó.");

                }

                if (appointment.Status == AppointmentStatus.Completed)

                {

                    return ApiResponse.Fail("Không thể hủy lịch hẹn đã hoàn thành khám.");

                }

                return ApiResponse.Fail($"Không thể hủy lịch hẹn ở trạng thái hiện tại ({appointment.Status}).");

            }

            var now = DateTime.UtcNow;

            var oldStatus = appointment.Status;

            appointment.Status = AppointmentStatus.Cancelled;

            appointment.CancelledAt = now;

            appointment.CancellationReason = $"[Lễ tân hủy] {reason.Trim()}";

            appointment.UpdatedAt = now;

            var historyNote = $"Lễ tân/Nhân viên hủy lịch: {reason.Trim()}";

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.Cancelled,

                ChangedByUserId = staffUserId,

                Note = historyNote,

                ChangedAt = now

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // Expire any pending change proposals atomically

            var pendingProposals = await _dbContext.AppointmentChangeProposals

                .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)

                .ToListAsync(ct);

            foreach (var p in pendingProposals)

            {

                p.Status = ProposalStatus.Expired;

                p.RespondedAt = now;

                p.Reason = (string.IsNullOrWhiteSpace(p.Reason) ? "" : p.Reason + " ") + "[Lễ tân hủy lịch hẹn gốc]";

            }

            // P2: Đồng bộ trạng thái FollowUpSchedule nếu có liên kết

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && linkedFollowUp.Status != "Completed")

            {

                linkedFollowUp.Status = "Cancelled";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)

            {

                await tx.CommitAsync(ct);

                committed = true;

            }

        }

        catch

        {

            if (tx != null && !committed)

            {

                await tx.RollbackAsync(ct);

            }

            throw;

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

        // Post-commit side effect: send notification (does not rollback or fail the main operation if delivery fails)

        try

        {

            await _notificationService.CreateNotificationAsync(

                appointment!.PatientUserId,

                appointmentId,

                "AppointmentCancelledByStaff",

                "Lịch hẹn đã được hủy bởi phòng khám",

                $"Lịch hẹn {appointment.AppointmentCode} đã được nhân viên phòng khám hỗ trợ hủy. Lý do: {reason.Trim()}. Bạn không bị tính điểm phạt gậy vi phạm.",

                ct);

        }

        catch (Exception notifEx)

        {

            _logger.LogWarning(notifEx, "Failed to send notification for staff cancelled appointment {AppointmentId}", appointmentId);

        }

        return ApiResponse.Ok("Đã hủy lịch hẹn thành công (không tính điểm phạt cho bệnh nhân).");

    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDepartmentRequestsAsync(

        long managerUserId,

        CancellationToken ct = default)

    {

        var managedDepartmentIds = await _dbContext.Departments

            .AsNoTracking()

            .Where(d => d.ManagerUserId == managerUserId)

            .Select(d => d.DepartmentId)

            .ToListAsync(ct);

        if (!managedDepartmentIds.Any())

        {

            return ApiResponse<List<AppointmentDto>>.Ok(new List<AppointmentDto>(), "Bạn chưa được chỉ định làm Trưởng khoa.");

        }

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.DepartmentId != null &&
                        managedDepartmentIds.Contains(a.DepartmentId.Value) &&
                        a.Status == AppointmentStatus.PendingDepartmentReview)
            .OrderBy(a => a.ScheduledStart);

        var list = await MapToDtoListAsync(query, ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);

    }

    public async Task<ApiResponse<List<DentistOptionDto>>> GetAvailableDentistsForAppointmentAsync(

        long appointmentId,

        long managerUserId,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .AsNoTracking()

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || !appointment.DepartmentId.HasValue || !appointment.ScheduledStart.HasValue || !appointment.ScheduledEnd.HasValue)

        {

            return ApiResponse<List<DentistOptionDto>>.Fail("Không tìm thấy thông tin lịch hẹn hoặc chưa có thời gian khám.");

        }

        var deptId = appointment.DepartmentId.Value;

        var start = appointment.ScheduledStart.Value;

        var end = appointment.ScheduledEnd.Value;

        var date = start.Date;

        var dentistsInDept = await _dbContext.DentistDepartments

            .AsNoTracking()

            .Where(dd => dd.DepartmentId == deptId && dd.Status == "Active")

            .Include(dd => dd.DentistUser)

                .ThenInclude(d => d.User)

            .ToListAsync(ct);

        var candidateIds = dentistsInDept.Select(dd => dd.DentistUserId).ToList();

        var targetDateOnly = DateOnly.FromDateTime(date);

        var startTimeOnly = TimeOnly.FromDateTime(start);

        var endTimeOnly = TimeOnly.FromDateTime(end);

        // Work schedule check: TimeOnly directly translated by EF Core

        var scheduledDentistIds = await _dbContext.DentistWorkSchedules

            .AsNoTracking()

            .Where(ws => candidateIds.Contains(ws.DentistUserId) &&

                         ws.DepartmentId == deptId &&

                         ws.WorkDate == targetDateOnly &&

                         ws.Status == "Scheduled" &&

                         ws.StartTime <= startTimeOnly &&

                         ws.EndTime >= endTimeOnly)

            .Select(ws => ws.DentistUserId)

            .ToListAsync(ct);

        // Unavailabilities

        var unavailableDentistIds = await _dbContext.DentistAvailabilities

            .AsNoTracking()

            .Where(da => candidateIds.Contains(da.DentistUserId) &&

                         da.AvailabilityStatus != DentistAvailabilityStatus.Available &&

                         da.StartDateTime < end &&

                         (da.EndDateTime == null || da.EndDateTime > start))

            .Select(da => da.DentistUserId)

            .Distinct()

            .ToListAsync(ct);

        // Appointment conflicts

        var occupiedStatuses = new[]

        {

            AppointmentStatus.Confirmed,

            AppointmentStatus.CheckedIn,

            AppointmentStatus.Waiting,

            AppointmentStatus.Called,

            AppointmentStatus.InService

        };

        var conflictedDentistIds = await _dbContext.Appointments

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

            .Select(dd =>

            {

                var isScheduled = scheduledDentistIds.Contains(dd.DentistUserId);

                var isUnavailable = unavailableDentistIds.Contains(dd.DentistUserId);

                var hasConflict = conflictedDentistIds.Contains(dd.DentistUserId);

                var isAvailable = isScheduled && !isUnavailable && !hasConflict;

                return new DentistOptionDto

                {

                    UserId = dd.DentistUserId,

                    FullName = dd.DentistUser.User.FullName,

                    EmployeeCode = dd.DentistUser.User.EmployeeCode,

                    Specialization = dd.DentistUser.Qualification,

                    Qualification = dd.DentistUser.Qualification,

                    YearsOfExperience = dd.DentistUser.YearsOfExperience,

                    Biography = dd.DentistUser.Biography,

                    IsAvailable = isAvailable

                };

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

        var conflictingAppointments = await _dbContext.Appointments

            .AsNoTracking()

            .Where(a => occupiedStatuses.Contains(a.Status) &&

                        a.ScheduledStart < end &&

                        a.ScheduledEnd > start)

            .ToListAsync(ct);

        var occupiedRoomIds = conflictingAppointments

            .Where(a => a.RoomId.HasValue)

            .Select(a => a.RoomId!.Value)

            .ToHashSet();

        var occupiedChairIds = conflictingAppointments

            .Where(a => a.DentalChairId.HasValue)

            .Select(a => a.DentalChairId!.Value)

            .ToHashSet();

        var allRooms = await _dbContext.Rooms

            .AsNoTracking()

            .Include(r => r.DentalChairs)

            .Where(r => r.Status == "Active" && (departmentId <= 0 || r.DepartmentId == departmentId))

            .OrderBy(r => r.RoomCode)

            .ToListAsync(ct);

        // A room is available if:

        // 1. For exclusive rooms: no appointment is occupying the room during the slot

        // 2. For shared (multi-chair) rooms: at least one active chair is free in that room

        var availableRooms = allRooms

            .Where(r =>

            {

                if (r.IsExclusive)

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

                IsExclusive = r.IsExclusive

            })

            .ToList();

        var availableRoomIds = availableRooms.Select(r => r.RoomId).ToHashSet();

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

        var dentistsInDept = await _dbContext.DentistDepartments

            .AsNoTracking()

            .Where(dd => dd.DepartmentId == departmentId && dd.Status == "Active")

            .Include(dd => dd.DentistUser)

                .ThenInclude(d => d.User)

            .Select(dd => new DentistOptionDto

            {

                UserId = dd.DentistUserId,

                FullName = dd.DentistUser.User.FullName,

                Qualification = dd.DentistUser.Qualification,

                YearsOfExperience = dd.DentistUser.YearsOfExperience,

                Biography = dd.DentistUser.Biography

            })

            .ToListAsync(ct);

        var result = new AvailableResourcesDto

        {

            Rooms = availableRooms,

            Chairs = chairs,

            Dentists = dentistsInDept

        };

        return ApiResponse<AvailableResourcesDto>.Ok(result);

    }

    public async Task<ApiResponse<AppointmentDto>> ManagerConfirmAsync(

        long appointmentId,

        long managerUserId,

        ManagerConfirmRequest request,

        CancellationToken ct = default)

    {

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        IDisposable? lockScope = null;

        bool committed = false;

        try

        {

            var appointment = await _dbContext.Appointments

                .Include(a => a.Department)

                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null)

            {

                return ApiResponse<AppointmentDto>.Fail("Không tìm thấy lịch hẹn.");

            }

            if (appointment.Status != AppointmentStatus.PendingDepartmentReview && appointment.Status != AppointmentStatus.PendingReceptionReview)

            {

                return ApiResponse<AppointmentDto>.Fail($"Không thể xác nhận lịch hẹn ở trạng thái {appointment.Status}.");

            }

            var callerUser = await _dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == managerUserId, ct);

            bool isPrivileged = callerUser != null && (callerUser.Role == UserRole.Receptionist || callerUser.Role == UserRole.SystemAdministrator || callerUser.Role == UserRole.DepartmentManager);

            if (!isPrivileged && appointment.Department?.ManagerUserId != managerUserId)

            {

                return ApiResponse<AppointmentDto>.Fail("Bạn không có quyền xác nhận lịch hẹn này.");

            }

            // CRITICAL BUSINESS RULE: If Patient chose a Specific Dentist, Manager cannot silently assign a different dentist!

            if (appointment.RequestedDentistUserId.HasValue &&

                appointment.RequestedDentistUserId.Value != request.AssignedDentistUserId)

            {

                return ApiResponse<AppointmentDto>.Fail("Bệnh nhân đã yêu cầu một Bác sĩ cụ thể. Để thay đổi Bác sĩ, bạn phải tạo Đề xuất thay đổi (Proposal) để Bệnh nhân đồng ý.");

            }

            // CRITICAL CLINICAL RULE: Validate Assigned Dentist belongs to this Department

            var isDentistInDept = await _dbContext.DentistDepartments

                .AsNoTracking()

                .AnyAsync(dd => dd.DentistUserId == request.AssignedDentistUserId && dd.DepartmentId == appointment.DepartmentId && dd.Status == "Active", ct);

            if (!isDentistInDept)

            {

                return ApiResponse<AppointmentDto>.Fail("Bác sĩ được chỉ định không thuộc chuyên khoa này hoặc không hoạt động.");

            }

            // CRITICAL CLINICAL RULE: Validate Requested Service belongs to this Department

            if (appointment.RequestedServiceId.HasValue)

            {

                var validService = await _dbContext.DepartmentServices

                    .AsNoTracking()

                    .AnyAsync(s => s.DepartmentServiceId == appointment.RequestedServiceId.Value && s.DepartmentId == appointment.DepartmentId && s.Status == "Active", ct);

                if (!validService)

                {

                    return ApiResponse<AppointmentDto>.Fail("Dịch vụ yêu cầu không thuộc chuyên khoa này.");

                }

            }

            var finalStart = request.ScheduledStart ?? appointment.ScheduledStart;

            var finalEnd = request.ScheduledEnd ?? appointment.ScheduledEnd;

            if (!finalStart.HasValue || !finalEnd.HasValue)

            {

                return ApiResponse<AppointmentDto>.Fail("Thời gian khám không hợp lệ.");

            }

            var targetDate = finalStart.Value.Date;

            // 1. Validate Room

            var room = await _dbContext.Rooms

                .AsNoTracking()

                .FirstOrDefaultAsync(r => r.RoomId == request.RoomId && r.Status == "Active", ct);

            if (room == null)

            {

                return ApiResponse<AppointmentDto>.Fail("Phòng khám không hợp lệ hoặc không hoạt động.");

            }

            if (room.DepartmentId.HasValue && room.DepartmentId.Value != appointment.DepartmentId)

            {

                return ApiResponse<AppointmentDto>.Fail("Phòng khám không thuộc chuyên khoa của lịch hẹn này.");

            }

            // 2. Validate Chair (Mandatory allocation by Department Manager)

            if (!request.DentalChairId.HasValue || request.DentalChairId.Value <= 0)

            {

                return ApiResponse<AppointmentDto>.Fail("Vui lòng chọn ghế nha khoa điều trị cho ca khám.");

            }

            var chair = await _dbContext.DentalChairs

                .AsNoTracking()

                .FirstOrDefaultAsync(c => c.DentalChairId == request.DentalChairId.Value &&

                                          c.RoomId == request.RoomId &&

                                          c.Status == "Active", ct);

            if (chair == null)

            {

                return ApiResponse<AppointmentDto>.Fail("Ghế nha khoa không thuộc phòng khám đã chọn hoặc không hoạt động.");

            }

            // Phase 2: Multi-Resource Concurrency Lock in Canonical Order (Deadlock-Free, Cross-Department Safe)

            var lockKeys = new List<string>

            {

                SqlAppLockHelper.ApptKey(appointmentId),

                SqlAppLockHelper.DentistMasterKey(request.AssignedDentistUserId),

                SqlAppLockHelper.DentistKey(request.AssignedDentistUserId, targetDate),

                SqlAppLockHelper.PatientKey(appointment.PatientUserId, targetDate)

            };

            if (appointment.DepartmentId.HasValue)

            {

                lockKeys.Add(SqlAppLockHelper.DeptQueueKey(appointment.DepartmentId.Value, targetDate));

            }

            if (request.DentalChairId.HasValue && request.DentalChairId.Value > 0)

            {

                lockKeys.Add(SqlAppLockHelper.ChairKey(request.DentalChairId.Value, targetDate));

            }

            if (room.IsExclusive)

            {

                lockKeys.Add(SqlAppLockHelper.RoomKey(request.RoomId, targetDate));

            }

            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            // Revalidate appointment state within the lock

            var freshAppt = await _dbContext.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (freshAppt == null || (freshAppt.Status != AppointmentStatus.PendingDepartmentReview && freshAppt.Status != AppointmentStatus.PendingReceptionReview))

            {

                return ApiResponse<AppointmentDto>.Fail($"Không thể xác nhận lịch hẹn ở trạng thái {freshAppt?.Status ?? "Không xác định"}.");

            }

            var occupiedStatuses = new[]

            {

                AppointmentStatus.Confirmed,

                AppointmentStatus.CheckedIn,

                AppointmentStatus.Waiting,

                AppointmentStatus.Called,

                AppointmentStatus.InService

            };

            // 1. REVALIDATE DENTIST WORK SHIFT COVERAGE (Under lock)

            var targetDateOnly = DateOnly.FromDateTime(targetDate);

            var startTimeOnly = TimeOnly.FromDateTime(finalStart.Value);

            var endTimeOnly = TimeOnly.FromDateTime(finalEnd.Value);

            var doctorShifts = await _dbContext.DentistWorkSchedules

                .AsNoTracking()

                .Where(ws => ws.DentistUserId == request.AssignedDentistUserId &&

                             ws.WorkDate == targetDateOnly &&

                             (appointment.DepartmentId == null || ws.DepartmentId == appointment.DepartmentId.Value))

                .ToListAsync(ct);

            if (!doctorShifts.Any())

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse<AppointmentDto>.Fail("Bác sĩ không có ca làm việc trong ngày được chọn.");

            }

            var activeShifts = doctorShifts.Where(ws => ws.Status == "Scheduled").ToList();

            if (!activeShifts.Any())

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse<AppointmentDto>.Fail("Ca làm việc của Bác sĩ đã bị hủy.");

            }

            var coveringShift = activeShifts.FirstOrDefault(ws => ws.StartTime <= startTimeOnly && ws.EndTime >= endTimeOnly);

            if (coveringShift == null)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                var shiftTimes = string.Join(", ", activeShifts.Select(s => $"{s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm}"));

                return ApiResponse<AppointmentDto>.Fail($"Giờ khám nằm ngoài khung giờ làm việc của Bác sĩ (ca làm việc: {shiftTimes}).");

            }

            // 2. REVALIDATE DENTIST CONFLICT (Across all departments and unavailabilities/leaves)

            bool dentistLeaveConflict = await _dbContext.DentistAvailabilities

                .AsNoTracking()

                .AnyAsync(da => da.DentistUserId == request.AssignedDentistUserId &&

                                da.AvailabilityStatus != DentistAvailabilityStatus.Available &&

                                da.StartDateTime < finalEnd.Value &&

                                (da.EndDateTime == null || da.EndDateTime > finalStart.Value), ct);

            if (dentistLeaveConflict)

            {

                return ApiResponse<AppointmentDto>.Fail("Bác sĩ đang bận hoặc nghỉ phép trong khoảng thời gian này.");

            }

            bool dentistConflict = await _dbContext.Appointments

                .AnyAsync(a => a.AppointmentId != appointmentId &&

                               a.AssignedDentistUserId == request.AssignedDentistUserId &&

                               occupiedStatuses.Contains(a.Status) &&

                               a.ScheduledStart < finalEnd.Value &&

                               a.ScheduledEnd > finalStart.Value, ct);

            if (dentistConflict)

            {

                return ApiResponse<AppointmentDto>.Fail("Bác sĩ đã có lịch hẹn khác trong khung giờ này.");

            }

            // 2. REVALIDATE PATIENT CONFLICT (Across all dentists and departments)

            bool patientConflict = await _dbContext.Appointments

                .AnyAsync(a => a.AppointmentId != appointmentId &&

                               a.PatientUserId == appointment.PatientUserId &&

                               occupiedStatuses.Contains(a.Status) &&

                               a.ScheduledStart < finalEnd.Value &&

                               a.ScheduledEnd > finalStart.Value, ct);

            if (patientConflict)

            {

                return ApiResponse<AppointmentDto>.Fail("Bệnh nhân đã có lịch hẹn khác trong khung giờ này.");

            }

            // 2. REVALIDATE ROOM CONFLICT (Only if room is designated as exclusive!)

            if (room.IsExclusive)

            {

                bool roomConflict = await _dbContext.Appointments

                    .AnyAsync(a => a.AppointmentId != appointmentId &&

                                   a.RoomId == request.RoomId &&

                                   occupiedStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < finalEnd.Value &&

                                   a.ScheduledEnd > finalStart.Value, ct);

                if (roomConflict)

                {

                    return ApiResponse<AppointmentDto>.Fail("Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này.");

                }

            }

            // 3. REVALIDATE CHAIR CONFLICT

            if (request.DentalChairId.HasValue && request.DentalChairId.Value > 0)

            {

                bool chairConflict = await _dbContext.Appointments

                    .AnyAsync(a => a.AppointmentId != appointmentId &&

                                   a.DentalChairId == request.DentalChairId.Value &&

                                   occupiedStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < finalEnd.Value &&

                                   a.ScheduledEnd > finalStart.Value, ct);

                if (chairConflict)

                {

                    return ApiResponse<AppointmentDto>.Fail("Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này.");

                }

            }

            // 4. GENERATE MONOTONIC QUEUE NUMBER (max sequence + 1 per department and target date)

            var existingQueueNumbers = await _dbContext.Appointments

                .Where(a => a.DepartmentId == appointment.DepartmentId &&

                            a.ScheduledStart != null &&

                            a.ScheduledStart.Value.Date == targetDate &&

                            a.QueueNumber != null &&

                            a.QueueNumber != "")

                .Select(a => a.QueueNumber!)

                .ToListAsync(ct);

            int maxSeq = 0;

            foreach (var qn in existingQueueNumbers)

            {

                if (!string.IsNullOrEmpty(qn) && qn.StartsWith("Q") && int.TryParse(qn.Substring(1), out int num))

                {

                    if (num > maxSeq) maxSeq = num;

                }

            }

            var queueNumber = $"Q{(maxSeq + 1):D3}";

            var oldStatus = appointment.Status;

            appointment.AssignedDentistUserId = request.AssignedDentistUserId;

            appointment.RoomId = request.RoomId;

            appointment.DentalChairId = request.DentalChairId;

            appointment.ScheduledStart = finalStart.Value;

            appointment.ScheduledEnd = finalEnd.Value;

            appointment.QueueNumber = queueNumber;

            appointment.Status = AppointmentStatus.Confirmed;

            appointment.ConfirmedAt = DateTime.UtcNow;

            appointment.UpdatedAt = DateTime.UtcNow;

            var history = new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = AppointmentStatus.Confirmed,

                ChangedByUserId = managerUserId,

                Note = $"Trưởng khoa xác nhận lịch hẹn. Số thứ tự khám: {queueNumber}.",

                ChangedAt = DateTime.UtcNow

            };

            _dbContext.AppointmentStatusHistories.Add(history);

            // P2: Đồng bộ trạng thái FollowUpSchedule khi Appointment được xác nhận

            var linkedFollowUp = await _dbContext.FollowUpSchedules

                .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

            if (linkedFollowUp != null && (linkedFollowUp.Status == "Pending" || linkedFollowUp.Status == "Scheduled" || linkedFollowUp.Status == "Proposed"))

            {

                linkedFollowUp.Status = "Scheduled";

            }

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null)

            {

                await tx.CommitAsync(ct);

                committed = true;

            }

            // Post-commit side effect: send notification (does not rollback or fail the main operation if delivery fails)

            try

            {

                await _notificationService.CreateNotificationAsync(

                    appointment.PatientUserId,

                    appointmentId,

                    "AppointmentConfirmed",

                    "Lịch khám đã được xác nhận!",

                    $"Lịch khám mã {appointment.AppointmentCode} đã được Trưởng khoa xác nhận thành công. Số thứ tự tiếp đón của bạn là {queueNumber}.",

                    ct);

            }

            catch (Exception notifEx)

            {

                _logger.LogWarning(notifEx, "Failed to send notification for confirmed appointment {AppointmentId}", appointmentId);

            }

            if (_emailService != null)

            {

                try

                {

                    var patient = await _dbContext.PatientProfiles

                        .AsNoTracking()

                        .Include(p => p.User)

                        .FirstOrDefaultAsync(p => p.UserId == appointment.PatientUserId, ct);

                    var recipientEmail = patient?.User?.Email;

                    if (!string.IsNullOrWhiteSpace(recipientEmail))

                    {

                        var patientName = !string.IsNullOrWhiteSpace(patient?.FullName)

                            ? patient.FullName

                            : "Quý khách";

                        var serviceName = "Khám răng miệng";

                        if (appointment.RequestedServiceId.HasValue)

                        {

                            var svc = await _dbContext.DepartmentServices

                                .AsNoTracking()

                                .FirstOrDefaultAsync(s => s.DepartmentServiceId == appointment.RequestedServiceId.Value, ct);

                            if (svc != null && !string.IsNullOrWhiteSpace(svc.ServiceName))

                            {

                                serviceName = svc.ServiceName;

                            }

                        }

                        var deptName = appointment.Department?.Name ?? "Chuyên khoa Răng Hàm Mặt";

                        var dentistName = "Bác sĩ phụ trách";

                        if (appointment.AssignedDentistUserId.HasValue)

                        {

                            var dentist = await _dbContext.StaffProfiles

                                .AsNoTracking()

                                .FirstOrDefaultAsync(s => s.UserId == appointment.AssignedDentistUserId.Value, ct);

                            if (dentist != null && !string.IsNullOrWhiteSpace(dentist.FullName))

                            {

                                dentistName = dentist.FullName;

                            }

                        }

                        var roomName = !string.IsNullOrWhiteSpace(room.RoomName) ? room.RoomName : "Phòng điều trị";

                        if (chair != null && !string.IsNullOrWhiteSpace(chair.ChairCode))

                        {

                            roomName = $"{roomName} (Ghế {chair.ChairCode})";

                        }

                        await _emailService.SendAppointmentConfirmedEmailAsync(

                            recipientEmail,

                            patientName,

                            appointment.AppointmentCode,

                            queueNumber,

                            serviceName,

                            deptName,

                            dentistName,

                            roomName,

                            finalStart.Value,

                            finalEnd.Value,

                            ct);

                    }

                }

                catch (Exception emailEx)

                {

                    _logger.LogWarning(emailEx, "Failed to send confirmation email for appointment {AppointmentId} to patient {PatientUserId}", appointmentId, appointment.PatientUserId);

                }

            }

            if (_reminderService != null)

            {

                try

                {

                    await _reminderService.ScheduleRemindersForAppointmentAsync(appointmentId, ct);

                }

                catch (Exception remEx)

                {

                    _logger.LogWarning(remEx, "Failed to schedule automated reminders for appointment {AppointmentId}", appointmentId);

                }

            }

            var dto = await MapToDtoAsync(appointmentId, ct);

            return ApiResponse<AppointmentDto>.Ok(dto!, "Xác nhận lịch hẹn thành công.");

        }

        catch (Exception ex)

        {

            if (tx != null && !committed) await tx.RollbackAsync(ct);

            _logger.LogError(ex, "Failed to confirm appointment {AppointmentId}", appointmentId);

            return ApiResponse<AppointmentDto>.Fail("Đã xảy ra lỗi trong quá trình xác nhận lịch hẹn.");

        }

        finally

        {

            lockScope?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

    }

    public async Task<ApiResponse> ManagerProposeChangeAsync(

        long appointmentId,

        long managerUserId,

        ManagerProposeChangeRequest request,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments

            .Include(a => a.Department)

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");

        }

        if (appointment.Status != AppointmentStatus.PendingDepartmentReview && appointment.Status != AppointmentStatus.PendingReceptionReview)

        {

            return ApiResponse.Fail($"Không thể tạo đề xuất thay đổi khi lịch hẹn ở trạng thái {appointment.Status}.");

        }

        var callerUser = await _dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == managerUserId, ct);

        bool isPrivileged = callerUser != null && (callerUser.Role == UserRole.Receptionist || callerUser.Role == UserRole.SystemAdministrator || callerUser.Role == UserRole.DepartmentManager);

        if (!isPrivileged && appointment.Department?.ManagerUserId != managerUserId)

        {

            return ApiResponse.Fail("Bạn không có quyền đề xuất thay đổi cho lịch hẹn này.");

        }

        if (string.IsNullOrWhiteSpace(request.Reason))

        {

            return ApiResponse.Fail("Vui lòng nhập lý do đề xuất thay đổi lịch hẹn.");

        }

        var now = DateTime.UtcNow;

        var proposal = new AppointmentChangeProposal

        {

            AppointmentId = appointmentId,

            ProposedByUserId = managerUserId,

            ProposedDentistUserId = request.ProposedDentistUserId,

            ProposedDepartmentId = request.ProposedDepartmentId,

            ProposedStart = request.ProposedStart,

            ProposedEnd = request.ProposedEnd,

            Reason = request.Reason.Trim(),

            Status = ProposalStatus.Pending,

            CreatedAt = now

        };

        _dbContext.AppointmentChangeProposals.Add(proposal);

        var oldStatus = appointment.Status;

        appointment.Status = AppointmentStatus.AwaitingPatientResponse;

        appointment.UpdatedAt = now;

        var history = new AppointmentStatusHistory

        {

            AppointmentId = appointmentId,

            OldStatus = oldStatus,

            NewStatus = AppointmentStatus.AwaitingPatientResponse,

            ChangedByUserId = managerUserId,

            Note = $"Trưởng khoa đề xuất thay đổi: {request.Reason.Trim()}",

            ChangedAt = now

        };

        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(

            appointment.PatientUserId,

            appointmentId,

            "ProposalCreated",

            "Đề xuất thay đổi lịch khám",

            $"Trưởng khoa đã đề xuất thay đổi cho lịch hẹn {appointment.AppointmentCode}. Lý do: {request.Reason.Trim()}. Vui lòng xem và phản hồi.",

            ct);

        return ApiResponse.Ok("Đã gửi đề xuất thay đổi lịch hẹn đến Bệnh nhân thành công.");

    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDentistTodayAppointmentsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var startOfDay = ClinicTimeHelper.Today.Date;
        var endOfDay = startOfDay.AddDays(1);

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
            .Where(a => (a.AssignedDentistUserId == dentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistUserId)) &&
                        a.ScheduledStart != null &&
                        a.ScheduledStart.Value >= startOfDay &&
                        a.ScheduledStart.Value < endOfDay &&
                        visibleStatuses.Contains(a.Status))
            .OrderBy(a => a.ScheduledStart);

        var list = await MapToDtoListAsync(query, ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDentistAppointmentsAsync(

        long dentistUserId,

        DateOnly? fromDate = null,

        DateOnly? toDate = null,

        CancellationToken ct = default)

    {

        var query = _dbContext.Appointments

            .AsNoTracking()

            .Where(a => a.AssignedDentistUserId == dentistUserId && a.ScheduledStart != null);

        if (fromDate.HasValue)

        {

            var startDt = fromDate.Value.ToDateTime(TimeOnly.MinValue);

            query = query.Where(a => a.ScheduledStart >= startDt);

        }

        if (toDate.HasValue)

        {

            var endDt = toDate.Value.ToDateTime(TimeOnly.MaxValue);

            query = query.Where(a => a.ScheduledStart <= endDt);

        }

        var visibleStatuses = new[]

        {

            AppointmentStatus.Confirmed,

            AppointmentStatus.CheckedIn,

            AppointmentStatus.Waiting,

            AppointmentStatus.Called,

            AppointmentStatus.InService,

            AppointmentStatus.Completed

        };

        var finalQuery = query
            .Where(a => visibleStatuses.Contains(a.Status))
            .OrderBy(a => a.ScheduledStart);

        var list = await MapToDtoListAsync(finalQuery, ct);
        return ApiResponse<List<AppointmentDto>>.Ok(list);

    }

    public async Task<ApiResponse<StartAppointmentResultDto>> DentistStartAppointmentAsync(
        long appointmentId,
        long dentistUserId,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);
        if (appointment == null)
        {
            return ApiResponse<StartAppointmentResultDto>.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        if (!appointment.AssignedDentistUserId.HasValue)
        {
            appointment.AssignedDentistUserId = dentistUserId;
        }
        else if (appointment.AssignedDentistUserId != dentistUserId)
        {
            var isSysAdmin = await _dbContext.UserAccounts.AnyAsync(u => u.UserId == dentistUserId && u.Role == UserRole.SystemAdministrator, ct);
            if (!isSysAdmin)
            {
                return ApiResponse<StartAppointmentResultDto>.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");
            }
        }

        if (appointment.Status != AppointmentStatus.CheckedIn && appointment.Status != AppointmentStatus.Waiting)
        {
            return ApiResponse<StartAppointmentResultDto>.Fail($"Không thể bắt đầu ca khám ở trạng thái '{appointment.Status}'. Bệnh nhân cần làm thủ tục check-in trước.");
        }

        if (!appointment.DentalChairId.HasValue || appointment.DentalChairId.Value <= 0)
        {
            return ApiResponse<StartAppointmentResultDto>.Fail("Ca khám chưa được phân bổ ghế điều trị hợp lệ. Vui lòng chọn ghế nha khoa trước khi tiếp nhận.");
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.InService;
        appointment.UpdatedAt = DateTime.UtcNow;

        _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
        {
            AppointmentId = appointment.AppointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.InService,
            ChangedByUserId = dentistUserId,
            Note = "Bác sĩ tiếp nhận bệnh nhân vào ghế điều trị.",
            ChangedAt = DateTime.UtcNow
        });

        // P2: Chuyển trạng thái FollowUpSchedule sang Completed khi ca khám hoàn tất
        var linkedFollowUp = await _dbContext.FollowUpSchedules
            .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);
        if (linkedFollowUp != null)
        {
            linkedFollowUp.Status = "Completed";
        }

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            appointment.PatientUserId,
            appointment.AppointmentId,
            "AppointmentInService",
            "Đang điều trị tại ghế",
            $"Bác sĩ đã bắt đầu thực hiện ca khám/thủ thuật cho lịch hẹn {appointment.AppointmentCode}.",
            ct);

        var resultDto = new StartAppointmentResultDto
        {
            AppointmentId = appointment.AppointmentId,
            PatientUserId = appointment.PatientUserId,
            Status = AppointmentStatus.InService,
            DentalChairId = appointment.DentalChairId
        };

        return ApiResponse<StartAppointmentResultDto>.Ok(resultDto, "Đã tiếp nhận bệnh nhân vào ghế điều trị thành công.");
    }

    public async Task<ApiResponse> DentistCompleteAppointmentAsync(

        long appointmentId,

        long dentistUserId,

        string? notes = null,

        CancellationToken ct = default)

    {

        var appointment = await _dbContext.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)

        {

            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");

        }

        if (appointment.AssignedDentistUserId != dentistUserId)

        {

            return ApiResponse.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");

        }

        if (appointment.Status != AppointmentStatus.InService)

        {

            if (appointment.Status == AppointmentStatus.CheckedIn)

            {

                return ApiResponse.Fail("Không thể hoàn thành ca khám khi chưa bắt đầu. Bác sĩ cần bấm 'Bắt đầu khám' (InService) trước khi hoàn tất.");

            }

            return ApiResponse.Fail($"Không thể hoàn thành ca khám ở trạng thái '{appointment.Status}'. Ca khám phải đang trong quá trình thực hiện (InService).");

        }

        var oldStatus = appointment.Status;

        appointment.Status = AppointmentStatus.Completed;

        appointment.CompletedAt = DateTime.UtcNow;

        appointment.UpdatedAt = DateTime.UtcNow;

        _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory

        {

            AppointmentId = appointment.AppointmentId,

            OldStatus = oldStatus,

            NewStatus = AppointmentStatus.Completed,

            ChangedByUserId = dentistUserId,

            Note = string.IsNullOrWhiteSpace(notes) ? "Bác sĩ đã hoàn tất ca khám và điều trị." : notes.Trim(),

            ChangedAt = DateTime.UtcNow

        });

        // P2: Chuyển trạng thái FollowUpSchedule sang Completed khi ca khám hoàn tất

        var linkedFollowUp = await _dbContext.FollowUpSchedules

            .FirstOrDefaultAsync(f => f.AppointmentId == appointment.AppointmentId, ct);

        if (linkedFollowUp != null)

        {

            linkedFollowUp.Status = "Completed";

        }

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(

            appointment.PatientUserId,

            appointment.AppointmentId,

            "AppointmentCompleted",

            "Ca khám đã hoàn tất",

            $"Ca khám mã {appointment.AppointmentCode} đã hoàn thành. Quý khách vui lòng để lại đánh giá dịch vụ để chúng tôi phục vụ tốt hơn!",

            ct);

        return ApiResponse.Ok("Đã hoàn thành ca khám thành công.");

    }

    private async Task<string> GenerateUniqueAppointmentCodeAsync(CancellationToken ct)

    {

        var datePrefix = $"APP-{DateTime.UtcNow:yyyyMMdd}-";

        for (int i = 0; i < 10; i++)

        {

            var randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToUpperInvariant();

            var code = $"{datePrefix}{randomHex}";

            bool exists = await _dbContext.Appointments.AnyAsync(a => a.AppointmentCode == code, ct);

            if (!exists)

            {

                return code;

            }

        }

        return $"{datePrefix}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    }

    private async Task<List<AppointmentDto>> MapToDtoListAsync(IQueryable<Appointment> query, CancellationToken ct)
    {
        var appointments = await query
            .AsNoTracking()
            .Include(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .Include(a => a.RequestedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.Room)
            .Include(a => a.DentalChair)
            .Include(a => a.Combo)
            .ToListAsync(ct);

        if (!appointments.Any()) return new List<AppointmentDto>();

        var apptIds = appointments.Select(a => a.AppointmentId).ToList();

        var proposals = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.ProposedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(p => p.ProposedDepartment)
            .Include(p => p.ProposedDentistUser)
                .ThenInclude(d => d!.User)
            .Where(p => apptIds.Contains(p.AppointmentId))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AppointmentChangeProposalDto
            {
                ProposalId = p.ProposalId,
                AppointmentId = p.AppointmentId,
                ProposedByUserId = p.ProposedByUserId,
                ProposedByName = p.ProposedByUser.StaffProfile != null ? p.ProposedByUser.StaffProfile.FullName : null,
                ProposedDepartmentId = p.ProposedDepartmentId,
                ProposedDepartmentName = p.ProposedDepartment != null ? p.ProposedDepartment.Name : null,
                ProposedDentistUserId = p.ProposedDentistUserId,
                ProposedDentistName = p.ProposedDentistUser != null ? p.ProposedDentistUser.User.FullName : null,
                ProposedStart = p.ProposedStart,
                ProposedEnd = p.ProposedEnd,
                Reason = p.Reason,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                RespondedAt = p.RespondedAt
            })
            .ToListAsync(ct);

        var proposalsByAppt = proposals.ToLookup(p => p.AppointmentId);

        var histories = await _dbContext.AppointmentStatusHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(h => h.ChangedByUser)
                .ThenInclude(u => u!.PatientProfile)
            .Where(h => apptIds.Contains(h.AppointmentId))
            .OrderBy(h => h.ChangedAt)
            .Select(h => new StatusHistoryDto
            {
                HistoryId = h.HistoryId,
                AppointmentId = h.AppointmentId,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByName = h.ChangedByUser != null
                    ? (h.ChangedByUser.StaffProfile != null ? h.ChangedByUser.StaffProfile.FullName : (h.ChangedByUser.PatientProfile != null ? h.ChangedByUser.PatientProfile.FullName : null))
                    : null,
                Note = h.Note,
                ChangedAt = h.ChangedAt
            })
            .ToListAsync(ct);

        var historiesByAppt = histories.ToLookup(h => h.AppointmentId);

        return appointments.Select(a => new AppointmentDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = a.AppointmentCode,
            PatientUserId = a.PatientUserId,
            PatientName = a.PatientUser.FullName,
            PatientCode = a.PatientUser.PatientCode,
            PatientPhone = a.PatientUser.User.PhoneNumber,
            DepartmentId = a.DepartmentId,
            DepartmentName = a.Department?.Name,
            RequestedServiceId = a.RequestedServiceId,
            ServiceName = a.RequestedService?.ServiceName,
            ServiceDuration = a.RequestedService?.DurationMinutes,
            ServicePrice = a.RequestedService?.Price,
            RequestedDentistUserId = a.RequestedDentistUserId,
            RequestedDentistName = a.RequestedDentistUser?.User.FullName,
            AssignedDentistUserId = a.AssignedDentistUserId,
            AssignedDentistName = a.AssignedDentistUser?.User.FullName,
            RoomId = a.RoomId,
            RoomName = a.Room?.RoomName ?? a.Room?.RoomCode,
            DentalChairId = a.DentalChairId,
            ChairCode = a.DentalChair?.ChairCode,
            ScheduledStart = a.ScheduledStart,
            ScheduledEnd = a.ScheduledEnd,
            ComboCode = a.ComboCode,
            ComboId = a.ComboId,
            ComboTitle = a.Combo?.Title,
            ReasonForVisit = a.ReasonForVisit,
            BookingSource = a.BookingSource,
            QueueNumber = a.QueueNumber,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            ConfirmedAt = a.ConfirmedAt,
            CheckedInAt = a.CheckedInAt,
            CompletedAt = a.CompletedAt,
            CancelledAt = a.CancelledAt,
            CancellationReason = a.CancellationReason,
            UpdatedAt = a.UpdatedAt,
            Proposals = proposalsByAppt[a.AppointmentId].ToList(),
            StatusHistories = historiesByAppt[a.AppointmentId].ToList()
        }).ToList();
    }

    private async Task<AppointmentDto?> MapToDtoAsync(long appointmentId, CancellationToken ct)

    {

        var a = await _dbContext.Appointments

            .AsNoTracking()

            .Include(a => a.PatientUser)

                .ThenInclude(p => p.User)

            .Include(a => a.Department)

            .Include(a => a.RequestedService)

            .Include(a => a.RequestedDentistUser)

                .ThenInclude(d => d!.User)

            .Include(a => a.AssignedDentistUser)

                .ThenInclude(d => d!.User)

            .Include(a => a.Room)

            .Include(a => a.DentalChair)
            .Include(a => a.Combo)

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (a == null) return null;

        var proposals = await _dbContext.AppointmentChangeProposals

            .AsNoTracking()

            .Include(p => p.ProposedByUser)

                .ThenInclude(u => u!.StaffProfile)

            .Include(p => p.ProposedDepartment)

            .Include(p => p.ProposedDentistUser)

                .ThenInclude(d => d!.User)

            .Where(p => p.AppointmentId == appointmentId)

            .OrderByDescending(p => p.CreatedAt)

            .Select(p => new AppointmentChangeProposalDto

            {

                ProposalId = p.ProposalId,

                AppointmentId = p.AppointmentId,

                ProposedByUserId = p.ProposedByUserId,

                ProposedByName = p.ProposedByUser.StaffProfile != null ? p.ProposedByUser.StaffProfile.FullName : null,

                ProposedDepartmentId = p.ProposedDepartmentId,

                ProposedDepartmentName = p.ProposedDepartment != null ? p.ProposedDepartment.Name : null,

                ProposedDentistUserId = p.ProposedDentistUserId,

                ProposedDentistName = p.ProposedDentistUser != null ? p.ProposedDentistUser.User.FullName : null,

                ProposedStart = p.ProposedStart,

                ProposedEnd = p.ProposedEnd,

                Reason = p.Reason,

                Status = p.Status,

                CreatedAt = p.CreatedAt,

                RespondedAt = p.RespondedAt

            })

            .ToListAsync(ct);

        var histories = await _dbContext.AppointmentStatusHistories

            .AsNoTracking()

            .Include(h => h.ChangedByUser)

                .ThenInclude(u => u!.StaffProfile)

            .Include(h => h.ChangedByUser)

                .ThenInclude(u => u!.PatientProfile)

            .Where(h => h.AppointmentId == appointmentId)

            .OrderBy(h => h.ChangedAt)

            .Select(h => new StatusHistoryDto

            {

                HistoryId = h.HistoryId,

                AppointmentId = h.AppointmentId,

                OldStatus = h.OldStatus,

                NewStatus = h.NewStatus,

                ChangedByUserId = h.ChangedByUserId,

                ChangedByName = h.ChangedByUser != null

                    ? (h.ChangedByUser.StaffProfile != null ? h.ChangedByUser.StaffProfile.FullName : (h.ChangedByUser.PatientProfile != null ? h.ChangedByUser.PatientProfile.FullName : null))

                    : null,

                Note = h.Note,

                ChangedAt = h.ChangedAt

            })

            .ToListAsync(ct);

        return new AppointmentDto

        {

            AppointmentId = a.AppointmentId,

            AppointmentCode = a.AppointmentCode,

            PatientUserId = a.PatientUserId,

            PatientName = a.PatientUser.FullName,

            PatientCode = a.PatientUser.PatientCode,

            PatientPhone = a.PatientUser.User.PhoneNumber,

            DepartmentId = a.DepartmentId,

            DepartmentName = a.Department?.Name,

            RequestedServiceId = a.RequestedServiceId,

            ServiceName = a.RequestedService?.ServiceName,

            ServiceDuration = a.RequestedService?.DurationMinutes,

            ServicePrice = a.RequestedService?.Price,

            RequestedDentistUserId = a.RequestedDentistUserId,

            RequestedDentistName = a.RequestedDentistUser?.User.FullName,

            AssignedDentistUserId = a.AssignedDentistUserId,

            AssignedDentistName = a.AssignedDentistUser?.User.FullName,

            RoomId = a.RoomId,

            RoomName = a.Room?.RoomName ?? a.Room?.RoomCode,

            DentalChairId = a.DentalChairId,

            ChairCode = a.DentalChair?.ChairCode,

            ScheduledStart = a.ScheduledStart,

            ScheduledEnd = a.ScheduledEnd,

            ComboCode = a.ComboCode,
            ComboId = a.ComboId,
            ComboTitle = a.Combo?.Title,
            ReasonForVisit = a.ReasonForVisit,

            BookingSource = a.BookingSource,

            QueueNumber = a.QueueNumber,

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

    public async Task<ApiResponse<List<ChairOptionDto>>> GetDentistRoomChairsAsync(

        long appointmentId,

        long dentistUserId,

        CancellationToken ct = default)

    {

        var appt = await _dbContext.Appointments

            .AsNoTracking()

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)

        {

            return ApiResponse<List<ChairOptionDto>>.Fail("Không tìm thấy thông tin lịch hẹn.");

        }

        if (appt.AssignedDentistUserId != dentistUserId)

        {

            return ApiResponse<List<ChairOptionDto>>.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");

        }

        long targetRoomId = appt.RoomId ?? 0;

        if (targetRoomId <= 0)

        {

            var defaultRoom = await _dbContext.Rooms

                .AsNoTracking()

                .FirstOrDefaultAsync(r => r.DepartmentId == appt.DepartmentId && r.Status == "Active", ct);

            if (defaultRoom != null) targetRoomId = defaultRoom.RoomId;

        }

        if (targetRoomId <= 0)

        {

            return ApiResponse<List<ChairOptionDto>>.Ok(new List<ChairOptionDto>());

        }

        var start = appt.ScheduledStart ?? DateTime.UtcNow;

        var end = appt.ScheduledEnd ?? start.AddMinutes(30);

        var activeStatuses = new[]

        {

            AppointmentStatus.Confirmed,

            AppointmentStatus.CheckedIn,

            AppointmentStatus.Waiting,

            AppointmentStatus.Called,

            AppointmentStatus.InService

        };

        var chairs = await _dbContext.DentalChairs

            .AsNoTracking()

            .Where(c => c.RoomId == targetRoomId && c.Status == "Active")

            .OrderBy(c => c.ChairCode)

            .ToListAsync(ct);

        var occupiedChairIds = await _dbContext.Appointments

            .AsNoTracking()

            .Where(a => a.AppointmentId != appointmentId &&

                        a.DentalChairId != null &&

                        activeStatuses.Contains(a.Status) &&

                        a.ScheduledStart < end &&

                        a.ScheduledEnd > start)

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

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)

        {

            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");

        }

        if (appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Withdrawn)

        {

            return ApiResponse.Fail("Ca khám đã hoàn tất hoặc đã hủy, vị trí ghế điều trị đã được khóa và không thể thay đổi.");

        }

        if (appt.AssignedDentistUserId != dentistUserId)

        {

            return ApiResponse.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");

        }

        var chair = await _dbContext.DentalChairs

            .AsNoTracking()

            .FirstOrDefaultAsync(c => c.DentalChairId == dentalChairId && c.Status == "Active", ct);

        if (chair == null)

        {

            return ApiResponse.Fail("Ghế nha khoa không tồn tại hoặc đã ngừng hoạt động.");

        }

        var room = await _dbContext.Rooms

            .AsNoTracking()

            .FirstOrDefaultAsync(r => r.RoomId == chair.RoomId && r.Status == "Active", ct);

        if (room == null)

        {

            return ApiResponse.Fail("Phòng khám không tồn tại hoặc đã ngừng hoạt động.");

        }

        if (appt.RoomId.HasValue && chair.RoomId != appt.RoomId.Value)

        {

            return ApiResponse.Fail("Ghế nha khoa đã chọn không thuộc phòng khám của ca này.");

        }

        var activeStatuses = new[]

        {

            AppointmentStatus.Confirmed,

            AppointmentStatus.CheckedIn,

            AppointmentStatus.Waiting,

            AppointmentStatus.Called,

            AppointmentStatus.InService

        };

        var isRelational = _dbContext.Database.IsRelational();

        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        IDisposable? apptLock = null;

        IDisposable? resourceLock = null;

        try

        {

            // Step 1: Acquire ApptKey first to freeze appointment state and prevent concurrent modification of ScheduledStart or Status

            apptLock = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.ApptKey(appointmentId) }, 5000, ct);

            // Step 2: Reload fresh state under ApptKey lock

            if (isRelational) await _dbContext.Entry(appt).ReloadAsync(ct);

            if (appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Withdrawn)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Ca khám đã hoàn tất hoặc đã hủy, vị trí ghế điều trị đã được khóa và không thể thay đổi.");

            }

            if (appt.AssignedDentistUserId != dentistUserId)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");

            }

            if (appt.RoomId.HasValue && chair.RoomId != appt.RoomId.Value)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail("Ghế nha khoa đã chọn không thuộc phòng khám của ca này.");

            }

            // Step 3: Extract guaranteed-fresh date and time from the stabilized appointment entity

            var start = appt.ScheduledStart ?? DateTime.UtcNow;

            var end = appt.ScheduledEnd ?? start.AddMinutes(30);

            var targetDate = start.Date;

            // Step 4: Acquire date-consistent resource locks (ChairKey and RoomKey) matching the true date

            var resourceKeys = new List<string>

            {

                SqlAppLockHelper.ChairKey(dentalChairId, targetDate)

            };

            if (room.IsExclusive)

            {

                resourceKeys.Add(SqlAppLockHelper.RoomKey(chair.RoomId, targetDate));

            }

            resourceLock = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, resourceKeys, 5000, ct);

            if (room.IsExclusive)

            {

                bool isRoomOccupied = await _dbContext.Appointments

                    .AnyAsync(a => a.AppointmentId != appointmentId &&

                                   a.RoomId == chair.RoomId &&

                                   activeStatuses.Contains(a.Status) &&

                                   a.ScheduledStart < end &&

                                   a.ScheduledEnd > start, ct);

                if (isRoomOccupied)

                {

                    if (tx != null) await tx.RollbackAsync(ct);

                    return ApiResponse.Fail($"Phòng {room.RoomName} là phòng độc quyền và hiện đang có ca khám khác sử dụng.");

                }

            }

            bool isOccupied = await _dbContext.Appointments

                .AnyAsync(a => a.AppointmentId != appointmentId &&

                               a.DentalChairId == dentalChairId &&

                               activeStatuses.Contains(a.Status) &&

                               a.ScheduledStart < end &&

                               a.ScheduledEnd > start, ct);

            if (isOccupied)

            {

                if (tx != null) await tx.RollbackAsync(ct);

                return ApiResponse.Fail($"Ghế {chair.ChairCode} hiện đang có bệnh nhân khác sử dụng. Vui lòng chọn ghế khác.");

            }

            appt.DentalChairId = dentalChairId;

            appt.RoomId = chair.RoomId;

            appt.UpdatedAt = DateTime.UtcNow;

            var oldStatus = appt.Status;

            var newStatus = appt.Status;

            if (appt.Status == AppointmentStatus.CheckedIn)

            {

                newStatus = AppointmentStatus.InService;

                appt.Status = newStatus;

            }

            _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory

            {

                AppointmentId = appointmentId,

                OldStatus = oldStatus,

                NewStatus = newStatus,

                ChangedByUserId = dentistUserId,

                Note = newStatus == AppointmentStatus.InService 

                    ? $"Bác sĩ chọn ghế điều trị {chair.ChairCode} và chuyển ca sang trạng thái đang điều trị (InService)."

                    : $"Bác sĩ cập nhật ghế điều trị {chair.ChairCode}.",

                ChangedAt = DateTime.UtcNow

            });

            await _dbContext.SaveChangesAsync(ct);

            if (tx != null) await tx.CommitAsync(ct);

            return ApiResponse.Ok($"Đã chọn ghế {chair.ChairCode} cho ca khám thành công.");

        }

        catch

        {

            if (tx != null) await tx.RollbackAsync(ct);

            throw;

        }

        finally

        {

            resourceLock?.Dispose();

            apptLock?.Dispose();

            if (tx != null) await tx.DisposeAsync();

        }

    }

    public Task<ApiResponse<AppointmentDto>> ReceptionistConfirmAsync(long appointmentId, long receptionistUserId, ManagerConfirmRequest request, CancellationToken ct = default)

        {

            return ManagerConfirmAsync(appointmentId, receptionistUserId, request, ct);

        }

        public Task<ApiResponse> ReceptionistProposeChangeAsync(long appointmentId, long receptionistUserId, ManagerProposeChangeRequest request, CancellationToken ct = default)

        {

            return ManagerProposeChangeAsync(appointmentId, receptionistUserId, request, ct);

        }

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

            .Include(a => a.RequestedService)

            .Include(a => a.TreatmentSessions)

                .ThenInclude(ts => ts.PerformedServices)

            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)

        {

            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");

        }

        if (appt.Status == AppointmentStatus.Completed || appt.Status == AppointmentStatus.Cancelled)

        {

            return ApiResponse.Fail($"Không thể bổ sung dịch vụ khi ca khám đã ở trạng thái {appt.Status}.");

        }

        if (appt.AssignedDentistUserId.HasValue && appt.AssignedDentistUserId.Value != dentistUserId)

        {

            var user = await _dbContext.UserAccounts.FirstOrDefaultAsync(u => u.UserId == dentistUserId, ct);

            var isStaffOrAdmin = user != null && (user.Role == UserRole.SystemAdministrator || user.Role == UserRole.DepartmentManager);

            if (!isStaffOrAdmin)

            {

                return ApiResponse.Fail("Bạn không phải là bác sĩ được phân công cho ca khám này.");

            }

        }

        var service = await _dbContext.DepartmentServices

            .AsNoTracking()

            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.ServiceId && s.Status == "Active", ct);

        if (service == null)

        {

            return ApiResponse.Fail("Dịch vụ không tồn tại hoặc đã ngừng hoạt động.");

        }

        var now = DateTime.UtcNow;

        var deptId = appt.DepartmentId ?? 1;

        // Get or create TreatmentSession for this appointment

        var session = appt.TreatmentSessions.FirstOrDefault();

        if (session == null)

        {

            var appointmentNote = $"Đợt khám {appt.AppointmentCode}";

            var plan = await _dbContext.TreatmentPlans

                .FirstOrDefaultAsync(tp => tp.PatientUserId == appt.PatientUserId &&

                                           tp.DepartmentId == deptId &&

                                           tp.TreatmentPlanItems.Any(tpi => tpi.Note == appointmentNote), ct);

            if (plan == null)

            {

                plan = new TreatmentPlan

                {

                    PatientUserId = appt.PatientUserId,

                    DepartmentId = deptId,

                    CreatedByDentistUserId = dentistUserId,

                    Status = "Completed",

                    CreatedAt = now

                };

                _dbContext.TreatmentPlans.Add(plan);

                await _dbContext.SaveChangesAsync(ct);

            }

            var item = await _dbContext.TreatmentPlanItems

                .FirstOrDefaultAsync(tpi => tpi.TreatmentPlanId == plan.TreatmentPlanId && tpi.Note == appointmentNote, ct);

            if (item == null)

            {

                var maxSeq = await _dbContext.TreatmentPlanItems

                    .Where(tpi => tpi.TreatmentPlanId == plan.TreatmentPlanId)

                    .Select(tpi => (int?)tpi.SequenceNo)

                    .MaxAsync(ct) ?? 0;

                item = new TreatmentPlanItem

                {

                    TreatmentPlanId = plan.TreatmentPlanId,

                    SequenceNo = maxSeq + 1,

                    Status = "Completed",

                    AssignedDentistUserId = dentistUserId,

                    Note = appointmentNote

                };

                _dbContext.TreatmentPlanItems.Add(item);

                await _dbContext.SaveChangesAsync(ct);

            }

            session = new TreatmentSession

            {

                TreatmentPlanItemId = item.TreatmentPlanItemId,

                AppointmentId = appointmentId,

                DentistUserId = dentistUserId,

                SessionDate = now,

                TreatmentResult = "Thực hiện dịch vụ nha khoa",

                ClinicalNote = $"Phiên điều trị cho ca khám {appt.AppointmentCode}"

            };

            _dbContext.TreatmentSessions.Add(session);

            await _dbContext.SaveChangesAsync(ct);

        }

        // If session.PerformedServices is empty and appt.RequestedService exists, add requested service as base

        if (session.PerformedServices == null || !session.PerformedServices.Any())

        {

            if (appt.RequestedService != null)

            {

                var baseService = new PerformedService

                {

                    TreatmentSessionId = session.TreatmentSessionId,

                    DepartmentServiceId = appt.RequestedService.DepartmentServiceId,

                    Quantity = 1,

                    UnitPrice = appt.RequestedService.Price,

                    Amount = appt.RequestedService.Price

                };

                _dbContext.PerformedServices.Add(baseService);

            }

        }

        // Add additional performed service

        var qty = request.Quantity > 0 ? request.Quantity : 1;

        var performedService = new PerformedService

        {

            TreatmentSessionId = session.TreatmentSessionId,

            DepartmentServiceId = service.DepartmentServiceId,

            Quantity = qty,

            UnitPrice = service.Price,

            Amount = qty * service.Price

        };

        _dbContext.PerformedServices.Add(performedService);

        // Update dental chair if provided

        if (request.DentalChairId.HasValue && request.DentalChairId.Value > 0)

        {

            var chair = await _dbContext.DentalChairs.FirstOrDefaultAsync(c => c.DentalChairId == request.DentalChairId.Value, ct);

            if (chair != null)

            {

                appt.DentalChairId = chair.DentalChairId;

                appt.RoomId = chair.RoomId;

            }

        }

        // Delegate to another dentist if requested

        if (request.ExecutionMode == "delegate" && request.TargetDentistUserId.HasValue && request.TargetDentistUserId.Value > 0)

        {

            appt.AssignedDentistUserId = request.TargetDentistUserId.Value;

        }

        appt.UpdatedAt = now;

        var modeDesc = request.ExecutionMode == "delegate" ? "Chuyển giao bác sĩ khác" : "Bác sĩ tự thực hiện tại ghế";

        _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory

        {

            AppointmentId = appointmentId,

            OldStatus = appt.Status,

            NewStatus = appt.Status,

            ChangedByUserId = dentistUserId,

            Note = $"Bổ sung dịch vụ thực hiện: {service.ServiceName} ({modeDesc}). {request.Notes?.Trim()}",

            ChangedAt = now

        });

        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse.Ok($"Đã bổ sung dịch vụ {service.ServiceName} vào ca khám thành công! Dịch vụ đã được tự động đồng bộ vào phiên điều trị và hóa đơn viện phí.");

    }

    public async Task<ApiResponse<List<BookingComboDto>>> GetAvailableCombosAsync(CancellationToken ct = default)
    {
        var combos = await _dbContext.ServiceCombos
            .AsNoTracking()
            .Include(c => c.Department)
            .Include(c => c.PrimaryService)
            .Where(c => c.Status == "Active")
            .OrderBy(c => c.ComboId)
            .ToListAsync(ct);

        var result = combos.Select(c => new BookingComboDto
        {
            ComboId = c.ComboId,
            ComboCode = c.ComboCode,
            Title = c.Title,
            Tagline = c.Tagline,
            Badge = c.Badge,
            IconClass = c.IconClass,
            Description = c.Description,
            DepartmentId = c.DepartmentId,
            DepartmentName = c.Department != null ? c.Department.Name : string.Empty,
            PrimaryServiceId = c.PrimaryServiceId,
            PrimaryServiceName = c.PrimaryService != null ? c.PrimaryService.ServiceName : string.Empty,
            OriginalPrice = c.OriginalPrice,
            ComboPrice = c.ComboPrice,
            DurationMinutes = c.DurationMinutes,
            IncludedServices = !string.IsNullOrWhiteSpace(c.IncludedServices)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(c.IncludedServices, (System.Text.Json.JsonSerializerOptions)null) ?? new List<string>()
                : new List<string>(),
            Benefits = !string.IsNullOrWhiteSpace(c.Benefits)
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(c.Benefits, (System.Text.Json.JsonSerializerOptions)null) ?? new List<string>()
                : new List<string>(),
            Status = c.Status
        }).ToList();

        return ApiResponse<List<BookingComboDto>>.Ok(result);
    }
}
