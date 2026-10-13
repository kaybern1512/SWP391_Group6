using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class BillingService : IBillingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<BillingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(
        long staffUserId,
        CreateInvoiceRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<InvoiceDto>.Fail("Dữ liệu tạo hóa đơn không hợp lệ.");

        if (request.AppointmentId <= 0)
            return ApiResponse<InvoiceDto>.Fail("Mã ca khám không hợp lệ.");

        var appointment = await _dbContext.Appointments
            .Include(a => a.PatientUser)
            .Include(a => a.Invoice)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null)
            return ApiResponse<InvoiceDto>.Fail("Không tìm thấy ca khám chỉ định.");

        if (appointment.Invoice != null)
            return ApiResponse<InvoiceDto>.Fail($"Ca khám này đã được lập hóa đơn mã {appointment.Invoice.InvoiceCode}.");

        if (request.Items == null || !request.Items.Any())
            return ApiResponse<InvoiceDto>.Fail("Vui lòng nhập ít nhất một dịch vụ hoặc mục viện phí.");

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description))
                return ApiResponse<InvoiceDto>.Fail("Tên dịch vụ / khoản phí không được để trống.");

            if (item.Quantity <= 0)
                return ApiResponse<InvoiceDto>.Fail($"Số lượng của '{item.Description}' phải lớn hơn 0.");

            if (item.UnitPrice < 0)
                return ApiResponse<InvoiceDto>.Fail($"Đơn giá của '{item.Description}' không thể âm.");
        }

        // Validate staff user ID or pick available staff profile
        var validStaffUserId = staffUserId;
        var staffExists = await _dbContext.StaffProfiles.AnyAsync(s => s.UserId == staffUserId, ct);
        if (!staffExists)
        {
            var defaultStaff = await _dbContext.StaffProfiles.FirstOrDefaultAsync(ct);
            if (defaultStaff != null)
            {
                validStaffUserId = defaultStaff.UserId;
            }
            else
            {
                return ApiResponse<InvoiceDto>.Fail("Không tìm thấy thông tin nhân viên tiếp nhận hợp lệ trong hệ thống.");
            }
        }

        var subTotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var discount = Math.Max(0, request.DiscountAmount);
        if (discount > subTotal)
        {
            discount = subTotal;
        }

        var discountedSubTotal = subTotal - discount;
        var taxPercent = Math.Max(0, request.TaxPercent);
        var taxAmount = Math.Round(discountedSubTotal * (taxPercent / 100m), 2);
        var totalAmount = Math.Round(discountedSubTotal + taxAmount, 2);

        var invoiceCode = await GenerateInvoiceCodeAsync(ct);

        var invoice = new Invoice
        {
            InvoiceCode = invoiceCode,
            AppointmentId = appointment.AppointmentId,
            GeneratedByUserId = validStaffUserId,
            TotalAmount = totalAmount,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Items)
        {
            invoice.InvoiceItems.Add(new InvoiceItem
            {
                PerformedServiceId = item.PerformedServiceId,
                Description = item.Description.Trim(),
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        // If VAT was specified, record it as a transparent item if requested
        if (taxPercent > 0 && taxAmount > 0)
        {
            invoice.InvoiceItems.Add(new InvoiceItem
            {
                PerformedServiceId = null,
                Description = $"Thuế giá trị gia tăng (VAT {taxPercent:G29}%)",
                Quantity = 1,
                UnitPrice = taxAmount
            });
        }

        _dbContext.Invoices.Add(invoice);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            await _notificationService.CreateNotificationAsync(
                appointment.PatientUserId,
                appointment.AppointmentId,
                "Billing",
                "Hóa đơn viện phí mới",
                $"Hóa đơn #{invoice.InvoiceCode} với tổng tiền {totalAmount:N0} đ đã được tạo cho ca khám của bạn.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi thông báo cho bệnh nhân khi tạo hóa đơn {InvoiceCode}", invoice.InvoiceCode);
        }

        return await GetInvoiceByIdAsync(invoice.InvoiceId, ct);
    }

    public async Task<ApiResponse<InvoiceDto>> ProcessPaymentAsync(
        long staffUserId,
        ProcessPaymentRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<InvoiceDto>.Fail("Dữ liệu thanh toán không hợp lệ.");

        if (request.InvoiceId <= 0)
            return ApiResponse<InvoiceDto>.Fail("Mã hóa đơn không hợp lệ.");

        if (request.Amount <= 0)
            return ApiResponse<InvoiceDto>.Fail("Số tiền thanh toán phải lớn hơn 0.");

        var invoice = await _dbContext.Invoices
            .Include(i => i.Payments)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .FirstOrDefaultAsync(i => i.InvoiceId == request.InvoiceId, ct);

        if (invoice == null)
            return ApiResponse<InvoiceDto>.Fail("Không tìm thấy hóa đơn cần thanh toán.");

        if (invoice.Status == "Paid")
            return ApiResponse<InvoiceDto>.Fail("Hóa đơn này đã được thanh toán toàn bộ trước đó.");

        if (invoice.Status == "Cancelled")
            return ApiResponse<InvoiceDto>.Fail("Hóa đơn này đã bị hủy, không thể thu tiền.");

        var alreadyPaid = invoice.Payments
            .Where(p => p.Status == "Successful")
            .Sum(p => p.Amount);

        var remaining = Math.Max(0, invoice.TotalAmount - alreadyPaid);

        if (request.Amount > remaining)
        {
            return ApiResponse<InvoiceDto>.Fail(
                $"Số tiền thanh toán ({request.Amount:N0} đ) vượt quá số tiền còn lại phải thu ({remaining:N0} đ).");
        }

        var normalizedMethod = NormalizeMethod(request.Method);

        var txnRef = string.IsNullOrWhiteSpace(request.TransactionReference)
            ? $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}-{invoice.InvoiceId}"
            : request.TransactionReference.Trim();

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            PaidByPatientUserId = invoice.Appointment.PatientUserId,
            Method = normalizedMethod,
            Amount = request.Amount,
            TransactionReference = txnRef,
            Status = "Successful",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);

        var totalPaidAfterThis = alreadyPaid + request.Amount;
        if (totalPaidAfterThis >= invoice.TotalAmount)
        {
            invoice.Status = "Paid";
        }
        else
        {
            invoice.Status = "PartiallyPaid";
        }

        // Khi thanh toán đủ -> Cuộc hẹn tự chuyển sang Completed
        if (invoice.Status == "Paid" && invoice.Appointment != null && invoice.Appointment.Status != AppointmentStatus.Completed)
        {
            var oldStatus = invoice.Appointment.Status;
            invoice.Appointment.Status = AppointmentStatus.Completed;
            invoice.Appointment.CompletedAt = DateTime.UtcNow;
            invoice.Appointment.UpdatedAt = DateTime.UtcNow;

            var history = new AppointmentStatusHistory
            {
                AppointmentId = invoice.Appointment.AppointmentId,
                OldStatus = oldStatus,
                NewStatus = AppointmentStatus.Completed,
                ChangedByUserId = staffUserId,
                Note = $"Thanh toán đủ hóa đơn {invoice.InvoiceCode}. Hoàn thành toàn bộ quy trình ca khám.",
                ChangedAt = DateTime.UtcNow
            };
            _dbContext.AppointmentStatusHistories.Add(history);
        }

        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var methodLabel = GetMethodDisplayName(normalizedMethod);
            await _notificationService.CreateNotificationAsync(
                invoice.Appointment.PatientUserId,
                invoice.AppointmentId,
                "Payment",
                "Xác nhận thanh toán thành công",
                $"Bạn đã thanh toán {request.Amount:N0} đ cho hóa đơn #{invoice.InvoiceCode} bằng hình thức {methodLabel}. Trạng thái hóa đơn: {(invoice.Status == "Paid" ? "Đã thanh toán đủ" : "Đã đặt cọc một phần")}.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gửi thông báo thanh toán cho hóa đơn {InvoiceCode}", invoice.InvoiceCode);
        }

        return await GetInvoiceByIdAsync(invoice.InvoiceId, ct);
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Department)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        if (invoice == null)
            return ApiResponse<InvoiceDto>.Fail("Không tìm thấy thông tin hóa đơn.");

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var dto = MapToInvoiceDto(invoice, clinic);
        return ApiResponse<InvoiceDto>.Ok(dto);
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByAppointmentIdAsync(long appointmentId, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Department)
            .FirstOrDefaultAsync(i => i.AppointmentId == appointmentId, ct);

        if (invoice == null)
            return ApiResponse<InvoiceDto>.Fail("Ca khám chưa có hóa đơn.");

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var dto = MapToInvoiceDto(invoice, clinic);
        return ApiResponse<InvoiceDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(
        string? status = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(i => i.Status == st);
        }

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            query = query.Where(i => i.CreatedAt >= start);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(i => i.CreatedAt <= end);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceCode.ToLower().Contains(term) ||
                i.Appointment.AppointmentCode.ToLower().Contains(term) ||
                i.Appointment.PatientUser.FullName.ToLower().Contains(term) ||
                (i.Appointment.PatientUser.PatientCode != null && i.Appointment.PatientUser.PatientCode.ToLower().Contains(term)));
        }

        var list = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var dtos = list.Select(i => MapToInvoiceDto(i, clinic)).ToList();
        return ApiResponse<List<InvoiceDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetPatientInvoicesAsync(long patientUserId, CancellationToken ct = default)
    {
        var list = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .Include(i => i.GeneratedByUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Department)
            .Where(i => i.Appointment.PatientUserId == patientUserId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var dtos = list.Select(i => MapToInvoiceDto(i, clinic)).ToList();
        return ApiResponse<List<InvoiceDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AppointmentBillingPreparationDto>> GetAppointmentBillingPreparationAsync(
        long appointmentId,
        CancellationToken ct = default)
    {
        var appt = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .Include(a => a.Invoice)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(ts => ts.PerformedServices)
                    .ThenInclude(ps => ps.DepartmentService)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)
            return ApiResponse<AppointmentBillingPreparationDto>.Fail("Không tìm thấy ca khám.");

        var dto = new AppointmentBillingPreparationDto
        {
            AppointmentId = appt.AppointmentId,
            AppointmentCode = appt.AppointmentCode,
            PatientUserId = appt.PatientUserId,
            PatientCode = appt.PatientUser?.PatientCode ?? string.Empty,
            PatientName = appt.PatientUser?.FullName ?? "Bệnh nhân",
            PatientPhoneNumber = appt.PatientUser?.EmergencyContact,
            PatientAddress = appt.PatientUser?.Address,
            DentistName = appt.AssignedDentistUser?.User?.FullName,
            DepartmentName = appt.Department?.Name,
            ScheduledStart = appt.ScheduledStart,
            ReasonForVisit = appt.ReasonForVisit,
            Status = appt.Status,
            HasInvoice = appt.Invoice != null,
            ExistingInvoiceId = appt.Invoice?.InvoiceId,
            ExistingInvoiceCode = appt.Invoice?.InvoiceCode
        };

        // Collect performed services from clinical sessions
        var performedItems = new List<BillingPreloadedItemDto>();
        if (appt.TreatmentSessions != null && appt.TreatmentSessions.Any())
        {
            foreach (var session in appt.TreatmentSessions)
            {
                if (session.PerformedServices != null)
                {
                    foreach (var ps in session.PerformedServices)
                    {
                        performedItems.Add(new BillingPreloadedItemDto
                        {
                            PerformedServiceId = ps.PerformedServiceId,
                            DepartmentServiceId = ps.DepartmentServiceId,
                            Description = ps.DepartmentService?.ServiceName ?? "Dịch vụ nha khoa",
                            Quantity = ps.Quantity > 0 ? ps.Quantity : 1,
                            UnitPrice = ps.UnitPrice > 0 ? ps.UnitPrice : (ps.DepartmentService?.Price ?? 0)
                        });
                    }
                }
            }
        }

        // If no performed services recorded, fallback to requested service
        if (!performedItems.Any() && appt.RequestedService != null)
        {
            performedItems.Add(new BillingPreloadedItemDto
            {
                PerformedServiceId = null,
                DepartmentServiceId = appt.RequestedServiceId,
                Description = appt.RequestedService.ServiceName,
                Quantity = 1,
                UnitPrice = appt.RequestedService.Price
            });
        }

        dto.SuggestedItems = performedItems;

        // Load available clinic services for receptionist to select additional items if needed
        var availableServices = await _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(s => s.Department)
            .Where(s => s.Status == "Active")
            .OrderBy(s => s.ServiceName)
            .Select(s => new ClinicServiceOptionDto
            {
                ServiceId = s.DepartmentServiceId,
                ServiceName = s.ServiceName,
                Price = s.Price,
                DepartmentName = s.Department.Name
            })
            .ToListAsync(ct);

        dto.AvailableServices = availableServices;

        return ApiResponse<AppointmentBillingPreparationDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<CompletedAppointmentForBillingDto>>> GetCompletedAppointmentsForBillingAsync(
        CancellationToken ct = default)
    {
        var billableStatuses = new[]
        {
            AppointmentStatus.InService,
            AppointmentStatus.Completed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting
        };

        var appts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(a => a.Department)
            .Include(a => a.Invoice)
            .Where(a => billableStatuses.Contains(a.Status))
            .OrderByDescending(a => a.ScheduledStart ?? a.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        var list = appts.Select(a => new CompletedAppointmentForBillingDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = a.AppointmentCode,
            PatientName = a.PatientUser?.FullName ?? "Bệnh nhân",
            PatientPhone = a.PatientUser?.EmergencyContact,
            DentistName = a.AssignedDentistUser?.User?.FullName ?? "Chưa phân bổ",
            DepartmentName = a.Department?.Name ?? "--",
            ScheduledStart = a.ScheduledStart,
            Status = a.Status,
            HasInvoice = a.Invoice != null,
            InvoiceCode = a.Invoice?.InvoiceCode,
            TotalAmount = a.Invoice?.TotalAmount,
            InvoiceStatus = a.Invoice?.Status
        }).ToList();

        return ApiResponse<List<CompletedAppointmentForBillingDto>>.Ok(list);
    }

    private async Task<string> GenerateInvoiceCodeAsync(CancellationToken ct)
    {
        var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"INV-{todayStr}-";

        var existingCodes = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.InvoiceCode.StartsWith(prefix))
            .Select(i => i.InvoiceCode)
            .ToListAsync(ct);

        int maxSeq = 0;
        foreach (var code in existingCodes)
        {
            var parts = code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var seq))
            {
                if (seq > maxSeq) maxSeq = seq;
            }
        }

        return $"{prefix}{(maxSeq + 1):D4}";
    }

    private static string NormalizeMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return "Cash";
        var m = method.Trim().ToLowerInvariant();
        return m switch
        {
            "cash" or "tienmat" or "tiền mặt" => "Cash",
            "banktransfer" or "chuyenkhoan" or "chuyển khoản" => "BankTransfer",
            "creditcard" or "the" or "thẻ" or "card" => "CreditCard",
            _ => "Cash"
        };
    }

    private static string GetMethodDisplayName(string method)
    {
        return method switch
        {
            "Cash" => "Tiền mặt",
            "BankTransfer" => "Chuyển khoản ngân hàng",
            "CreditCard" => "Thẻ tín dụng / Ghi nợ",
            "Online" => "Thanh toán trực tuyến",
            _ => method
        };
    }

    private static InvoiceDto MapToInvoiceDto(Invoice invoice, ClinicProfile? clinic)
    {
        var subTotal = invoice.InvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        var paidAmount = invoice.Payments.Where(p => p.Status == "Successful").Sum(p => p.Amount);
        var remainingAmount = Math.Max(0, invoice.TotalAmount - paidAmount);

        // Derive discount amount if total amount is less than subtotal
        var discountAmount = subTotal > invoice.TotalAmount ? (subTotal - invoice.TotalAmount) : 0m;

        return new InvoiceDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            AppointmentId = invoice.AppointmentId,
            AppointmentCode = invoice.Appointment?.AppointmentCode,
            PatientUserId = invoice.Appointment?.PatientUserId ?? 0,
            PatientCode = invoice.Appointment?.PatientUser?.PatientCode,
            PatientName = invoice.Appointment?.PatientUser?.FullName ?? "Bệnh nhân",
            PatientPhoneNumber = invoice.Appointment?.PatientUser?.EmergencyContact,
            PatientEmail = invoice.Appointment?.PatientUser?.User?.Email,
            PatientAddress = invoice.Appointment?.PatientUser?.Address,
            DentistName = invoice.Appointment?.AssignedDentistUser?.User?.FullName ?? "Chưa phân bổ",
            DepartmentName = invoice.Appointment?.Department?.Name ?? "Nha khoa",
            GeneratedByUserId = invoice.GeneratedByUserId,
            GeneratedByName = invoice.GeneratedByUser?.FullName ?? "Nhân viên thu ngân",
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            TaxPercent = 0,
            TaxAmount = 0,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            Status = invoice.Status,
            CreatedAt = invoice.CreatedAt,
            AppointmentDate = invoice.Appointment?.ScheduledStart,
            ReasonForVisit = invoice.Appointment?.ReasonForVisit,

            ClinicName = clinic?.Name ?? "NHA KHOA ĐA CHUYÊN KHOA DENTALCARE",
            ClinicAddress = clinic?.Address ?? "123 Đường Nguyễn Tri Phương, Phường 5, Quận 10, TP. Hồ Chí Minh",
            ClinicPhone = clinic?.PhoneNumber ?? "028 3822 9999",
            ClinicEmail = clinic?.Email ?? "contact@dentalcare.vn",

            Items = invoice.InvoiceItems.Select(item => new InvoiceItemDto
            {
                InvoiceItemId = item.InvoiceItemId,
                InvoiceId = item.InvoiceId,
                PerformedServiceId = item.PerformedServiceId,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Quantity * item.UnitPrice
            }).ToList(),

            Payments = invoice.Payments.OrderByDescending(p => p.CreatedAt).Select(p => new PaymentDto
            {
                PaymentId = p.PaymentId,
                InvoiceId = p.InvoiceId,
                PaidByPatientUserId = p.PaidByPatientUserId,
                PaidByPatientName = invoice.Appointment?.PatientUser?.FullName ?? "Bệnh nhân",
                Method = p.Method,
                MethodDisplayName = GetMethodDisplayName(p.Method),
                Amount = p.Amount,
                TransactionReference = p.TransactionReference,
                Status = p.Status,
                PaidAt = p.PaidAt,
                CreatedAt = p.CreatedAt
            }).ToList()
        };
    }
}
