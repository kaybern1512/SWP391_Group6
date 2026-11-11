using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace DentalClinic.Tests.EndToEnd;

public class CompleteServiceFlowIntegrationTests
{
    private readonly ITestOutputHelper _output;

    private static string ConnectionString
    {
        get
        {
            var envConn = Environment.GetEnvironmentVariable("DENTAL_CLINIC_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(envConn)) return envConn;

            var server = Environment.GetEnvironmentVariable("TEST_SQL_SERVER")
                         ?? Environment.GetEnvironmentVariable("DENTAL_CLINIC_SQL_INSTANCE")
                         ?? ".\\SQLEXPRESS";
            var dbName = Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_DB") ?? "DentalCare_Integration_Dev";
            return $"Server={server};Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;";
        }
    }

    public CompleteServiceFlowIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task CompleteWorkflow_PrimaryAndAdditionalServices_E2E_Success()
    {
        using var db = CreateDbContext();
        if (!await db.Database.CanConnectAsync())
        {
            _output.WriteLine("Database not reachable, skipping.");
            return;
        }

        var mockNotification = new Mock<INotificationService>();
        var mockApptLogger = new Mock<ILogger<AppointmentService>>();
        var mockBillingLogger = new Mock<ILogger<BillingService>>();
        var mockPrescLogger = new Mock<ILogger<PrescriptionService>>();

        var apptService = new AppointmentService(db, mockNotification.Object, mockApptLogger.Object);
        var billingService = new BillingService(db, mockNotification.Object, mockBillingLogger.Object);
        var prescService = new PrescriptionService(db, mockPrescLogger.Object);

        var patientUser = await db.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == "lehoangminh.test@dentalcare.com" || (u.Role == UserRole.Patient && u.Status == "Active"));
        long patientUserId = patientUser?.UserId ?? 5;

        var receptionistUser = await db.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == "receptionist@dentalcare.com" || (u.Role == UserRole.Receptionist && u.Status == "Active"));
        long receptionistUserId = receptionistUser?.UserId ?? 2;

        var dentistUser = await db.UserAccounts
            .FirstOrDefaultAsync(u => u.Email == "dentist@dentalcare.com" || (u.Role == UserRole.Dentist && u.Status == "Active"));
        long dentistUserId = dentistUser?.UserId ?? 3;

        long departmentId = 1;      // Khoa Răng Tổng Quát
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == departmentId);
        long managerUserId = dept?.ManagerUserId ?? 10;
        long primaryServiceId = 1;  // Khám tổng quát & Tư vấn (100.000 đ)
        long extraService1Id = 2;   // Lấy cao răng & Đánh bóng (250.000 đ)
        long extraService2Id = 6;   // Tẩy trắng răng Laser Whitening (2.000.000 đ)

        var testDate = DateTime.Today.AddDays(Random.Shared.Next(100, 500)).AddHours(9).AddMinutes(Random.Shared.Next(1, 50));

        var workDate = DateOnly.FromDateTime(testDate);
        var existingSchedule = await db.DentistWorkSchedules
            .FirstOrDefaultAsync(s => s.DentistUserId == dentistUserId && s.WorkDate == workDate);
        if (existingSchedule == null)
        {
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistUserId = dentistUserId,
                DepartmentId = departmentId,
                WorkDate = workDate,
                StartTime = new TimeOnly(7, 0, 0),
                EndTime = new TimeOnly(20, 0, 0),
                Status = "Scheduled"
            });
            await db.SaveChangesAsync();
        }

        // Clean any stale InService appointments for dentist to prevent double-booking block
        var staleInService = await db.Appointments
            .Where(a => a.AssignedDentistUserId == dentistUserId && a.Status == AppointmentStatus.InService)
            .ToListAsync();
        foreach (var s in staleInService)
        {
            s.Status = AppointmentStatus.Completed;
            s.CompletedAt = DateTime.UtcNow;
        }
        if (staleInService.Count > 0) await db.SaveChangesAsync();

        _output.WriteLine("======================================================================");
        _output.WriteLine("KHỞI CHẠY LUỒNG KHÁM HOÀN CHỈNH: DỊCH VỤ CHÍNH + DỊCH VỤ THÊM");
        _output.WriteLine("======================================================================");

        // =========================================================================
        // BƯỚC 1: Bệnh nhân đặt lịch khám dịch vụ chính
        // =========================================================================
        var createReq = new CreateAppointmentRequest
        {
            DepartmentId = departmentId,
            RequestedServiceId = primaryServiceId,
            ScheduledStart = testDate,
            ReasonForVisit = "Kiểm tra răng định kỳ & Tư vấn tổng quát"
        };
        var createRes = await apptService.CreatePatientAppointmentAsync(patientUserId, createReq);
        Assert.True(createRes.Success, createRes.Message);
        long apptId = createRes.Data!.AppointmentId;
        string apptCode = createRes.Data.AppointmentCode;
        _output.WriteLine($"[BƯỚC 1] Bệnh nhân đặt lịch thành công: Mã ca {apptCode} (Id: {apptId})");
        _output.WriteLine($"         Dịch vụ chính: Khám tổng quát & Tư vấn (100.000 đ) - Trạng thái: {createRes.Data.Status}");

        // =========================================================================
        // BƯỚC 2: Lễ tân rà soát & Chuyển Trưởng khoa
        // =========================================================================
        var fwdRes = await apptService.ReceptionistForwardAsync(apptId, receptionistUserId, "Chuyển Trưởng khoa phân bổ bác sĩ");
        Assert.True(fwdRes.Success, fwdRes.Message);
        _output.WriteLine($"[BƯỚC 2] Lễ tân duyệt chuyển Trưởng khoa - Trạng thái: PendingDepartmentReview");

        // =========================================================================
        // BƯỚC 3: Trưởng khoa phê duyệt, gán Bác sĩ, Phòng & Ghế
        // =========================================================================
        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistUserId,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(60)
        };
        var confirmRes = await apptService.ManagerConfirmAsync(apptId, receptionistUserId, confirmReq);
        Assert.True(confirmRes.Success, confirmRes.Message);
        _output.WriteLine($"[BƯỚC 3] Trưởng khoa duyệt ca & cấp STT: {confirmRes.Data!.QueueNumber} - Bác sĩ phụ trách: BS. Trần Văn Hùng");

        // =========================================================================
        // BƯỚC 4: Lễ tân Check-in khi bệnh nhân tới phòng khám
        // =========================================================================
        var checkInRes = await apptService.ReceptionistCheckInAsync(apptId, receptionistUserId, "Bệnh nhân đã đến quầy");
        Assert.True(checkInRes.Success, checkInRes.Message);
        _output.WriteLine($"[BƯỚC 4] Lễ tân Check-in bệnh nhân thành công - Trạng thái: CheckedIn");

        // =========================================================================
        // BƯỚC 5: Bác sĩ tiếp nhận ca khám tại ghế điều trị
        // =========================================================================
        var startRes = await apptService.DentistStartAppointmentAsync(apptId, dentistUserId);
        Assert.True(startRes.Success, startRes.Message);
        _output.WriteLine($"[BƯỚC 5] Bác sĩ tiếp nhận bệnh nhân vào ghế - Trạng thái: InService");

        // =========================================================================
        // BƯỚC 6: Bác sĩ thêm DỊCH VỤ BỔ SUNG 1: Lấy cao răng & Đánh bóng (250.000 đ)
        // =========================================================================
        var addSvc1Req = new DentistAddAdditionalServiceRequest
        {
            ServiceId = extraService1Id,
            Quantity = 1,
            ExecutionMode = "self",
            Notes = "Bệnh nhân có mảng bám vôi răng độ 2, đồng ý lấy cao răng tại ghế"
        };
        var addSvc1Res = await apptService.DentistAddAdditionalServiceAsync(apptId, dentistUserId, addSvc1Req);
        Assert.True(addSvc1Res.Success, addSvc1Res.Message);
        _output.WriteLine($"[BƯỚC 6] Bác sĩ bổ sung Dịch Vụ Thêm 1: Lấy cao răng & Đánh bóng (+250.000 đ)");

        // =========================================================================
        // BƯỚC 7: Bác sĩ thêm DỊCH VỤ BỔ SUNG 2: Tẩy trắng răng Laser Whitening (2.000.000 đ)
        // =========================================================================
        var addSvc2Req = new DentistAddAdditionalServiceRequest
        {
            ServiceId = extraService2Id,
            Quantity = 1,
            ExecutionMode = "self",
            Notes = "Bệnh nhân yêu cầu tẩy trắng răng Laser sau khi lấy cao răng"
        };
        var addSvc2Res = await apptService.DentistAddAdditionalServiceAsync(apptId, dentistUserId, addSvc2Req);
        Assert.True(addSvc2Res.Success, addSvc2Res.Message);
        _output.WriteLine($"[BƯỚC 7] Bác sĩ bổ sung Dịch Vụ Thêm 2: Tẩy trắng răng Laser Whitening (+2.000.000 đ)");

        // =========================================================================
        // BƯỚC 8: Bác sĩ kê đơn thuốc (gắn thẳng vào ca khám)
        // =========================================================================
        var presReq = new CreatePrescriptionRequest
        {
            AppointmentId = apptId,
            GeneralInstructions = "Tránh thức ăn quá nóng/lạnh trong 48h sau tẩy trắng, súc miệng hàng ngày",
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Kem chống ê buốt Sensodyne", Dosage = "1 tuýp", Frequency = "Đánh răng ngày 2 lần", Duration = "30 ngày" },
                new() { MedicationName = "Nước súc miệng Chlorhexidine 0.12%", Dosage = "1 chai 250ml", Frequency = "Súc miệng ngày 2 lần", Duration = "7 ngày" }
            }
        };
        var presRes = await prescService.CreatePrescriptionAsync(dentistUserId, presReq);
        Assert.True(presRes.Success, presRes.Message);
        _output.WriteLine($"[BƯỚC 8] Bác sĩ kê đơn thuốc thành công: #RX-{presRes.Data!.PrescriptionId} (2 loại thuốc)");

        // =========================================================================
        // BƯỚC 9: Bác sĩ hoàn thành ca khám
        // =========================================================================
        var completeRes = await apptService.DentistCompleteAppointmentAsync(apptId, dentistUserId, "Đã hoàn thành khám, lấy cao răng và tẩy trắng răng cho bệnh nhân");
        Assert.True(completeRes.Success, completeRes.Message);
        _output.WriteLine($"[BƯỚC 9] Bác sĩ hoàn tất ca khám - Trạng thái: Completed");

        // =========================================================================
        // BƯỚC 10: Lễ tân mở màn hình Lập Hóa Đơn & Kiểm tra dữ liệu tự động đồng bộ
        // =========================================================================
        var prepRes = await billingService.GetAppointmentBillingPreparationAsync(apptId);
        Assert.True(prepRes.Success, prepRes.Message);
        var billPrep = prepRes.Data!;
        _output.WriteLine($"[BƯỚC 10] Lễ tân mở màn hình Lập Hóa Đơn. Hệ thống tự động gom {billPrep.SuggestedItems.Count} dịch vụ:");

        decimal totalExpected = 0;
        foreach (var item in billPrep.SuggestedItems)
        {
            decimal lineTotal = item.Quantity * item.UnitPrice;
            totalExpected += lineTotal;
            _output.WriteLine($"          - {item.Description}: SL {item.Quantity} x {item.UnitPrice:N0} đ = {lineTotal:N0} đ");
        }

        // Kiểm tra phải có cả dịch vụ chính và 2 dịch vụ thêm
        Assert.Contains(billPrep.SuggestedItems, i => i.Description.Contains("Khám"));
        Assert.Contains(billPrep.SuggestedItems, i => i.Description.Contains("Lấy cao răng"));
        Assert.Contains(billPrep.SuggestedItems, i => i.Description.Contains("Tẩy trắng răng"));

        // Tổng tiền dự kiến: 100.000 + 250.000 + 2.000.000 = 2.350.000 đ
        Assert.Equal(2350000m, totalExpected);
        _output.WriteLine($"          ==> TỔNG CỘNG HÓA ĐƠN TỰ ĐỘNG GỘP: {totalExpected:N0} đ");

        // =========================================================================
        // BƯỚC 11: Lễ tân tạo hóa đơn và thanh toán viện phí
        // =========================================================================
        var invoiceReq = new CreateInvoiceRequest
        {
            AppointmentId = apptId,
            DiscountAmount = 100000, // Ưu đãi giảm 100k
            TaxPercent = 0,
            Note = "Hóa đơn khám & điều trị trọn gói",
            Items = billPrep.SuggestedItems.Select(s => new CreateInvoiceItemRequest
            {
                Description = s.Description,
                Quantity = s.Quantity,
                UnitPrice = s.UnitPrice,
                PerformedServiceId = s.PerformedServiceId
            }).ToList()
        };
        var invoiceRes = await billingService.CreateInvoiceAsync(receptionistUserId, invoiceReq);
        Assert.True(invoiceRes.Success, invoiceRes.Message);
        long invoiceId = invoiceRes.Data!.InvoiceId;
        string invoiceCode = invoiceRes.Data.InvoiceCode;
        _output.WriteLine($"[BƯỚC 11] Lễ tân khởi tạo hóa đơn thành công: {invoiceCode} (Id: {invoiceId})");
        _output.WriteLine($"          Tạm tính: {invoiceRes.Data.SubTotal:N0} đ - Giảm giá: {invoiceRes.Data.DiscountAmount:N0} đ - Cần thanh toán: {invoiceRes.Data.TotalAmount:N0} đ");

        // Thanh toán bằng VNPay
        var payReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Method = "VNPay",
            Amount = invoiceRes.Data.TotalAmount,
            Note = "Thanh toán thành công qua cổng VNPay QR"
        };
        var payRes = await billingService.ProcessPaymentAsync(receptionistUserId, payReq);
        Assert.True(payRes.Success, payRes.Message);
        Assert.Equal("Paid", payRes.Data!.Status);
        _output.WriteLine($"[BƯỚC 12] Thanh toán viện phí hoàn tất! Trạng thái hóa đơn: {payRes.Data.Status} (ĐÃ THANH TOÁN)");
        _output.WriteLine("======================================================================");
        _output.WriteLine("KIỂM THỬ E2E TOÀN BỘ LUỒNG THÀNH CÔNG 100%!");
        _output.WriteLine("======================================================================");
    }
}
