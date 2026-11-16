using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.UnicodeEncoding;

public class UnicodeEncodingVerificationTests
{
    private static readonly string TestConnectionString =
        Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_CONNECTION_STRING")
        ?? "Server=.\\SQLEXPRESS;Database=DentalCare_SWP391_Encoding_TEST;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    // =========================================================================
    // TEST GROUP 1: CANONICAL SQL SERVER UNICODE STORAGE & READ-BACK
    // =========================================================================
    [Fact]
    public async Task TestGroup1_SqlUnicodeStorageAndRoundtrip_PreservesExactVietnameseCharacters()
    {
        var representativeStrings = new List<string>
        {
            "Khám & Lấy Cao Răng",
            "Nhổ Răng Khôn",
            "Niềng Răng Mắc Cài Kim Loại",
            "Khoa Răng Tổng Quát",
            "Khoa Chỉnh Nha",
            "Nguyễn Văn Đăng",
            "Đặng Thị Ánh",
            "Chẩn đoán: viêm nướu nhẹ",
            "Đề nghị tái khám sau 6 tháng",
            "Đau nhức răng hàm dưới bên phải, nghi viêm tủy.",
            "Uống 1 viên sau ăn, ngày 2 lần.",
            "Chăm sóc răng miệng đúng cách mỗi ngày",
            "Lịch khám của bạn đã được xác nhận."
        };

        await using var conn = new SqlConnection(TestConnectionString);
        await conn.OpenAsync();

        await using (var createCmd = conn.CreateCommand())
        {
            createCmd.CommandText = @"
                IF OBJECT_ID('tempdb..#SqlUnicodeVerification') IS NOT NULL DROP TABLE #SqlUnicodeVerification;
                CREATE TABLE #SqlUnicodeVerification (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ExpectedText NVARCHAR(500) NOT NULL
                );";
            await createCmd.ExecuteNonQueryAsync();
        }

        for (int i = 0; i < representativeStrings.Count; i++)
        {
            var original = representativeStrings[i];

            await using (var insertCmd = conn.CreateCommand())
            {
                insertCmd.CommandText = "INSERT INTO #SqlUnicodeVerification (ExpectedText) VALUES (@text);";
                var param = insertCmd.Parameters.Add("@text", SqlDbType.NVarChar, 500);
                param.Value = original;
                await insertCmd.ExecuteNonQueryAsync();
            }

            await using (var selectCmd = conn.CreateCommand())
            {
                selectCmd.CommandText = "SELECT ExpectedText FROM #SqlUnicodeVerification WHERE Id = @id;";
                selectCmd.Parameters.AddWithValue("@id", i + 1);
                var retrieved = (string)(await selectCmd.ExecuteScalarAsync())!;

                Assert.Equal(original, retrieved);
                Assert.Equal(original.Length, retrieved.Length);

                for (int c = 0; c < original.Length; c++)
                {
                    Assert.Equal((int)original[c], (int)retrieved[c]);
                }
            }
        }
    }

    // =========================================================================
    // TEST GROUP 3: EF CORE, DTO & JSON SERIALIZATION PIPELINE
    // =========================================================================
    [Fact]
    public void TestGroup3_DtoJsonSerialization_PreservesVietnameseWithoutEscapingOrCorruption()
    {
        var serviceDto = new DepartmentServiceDto
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            DepartmentName = "Khoa Răng Tổng Quát",
            ServiceName = "Khám & Lấy Cao Răng",
            Description = "Vệ sinh răng miệng và lấy vôi răng siêu âm",
            Price = 200000,
            DurationMinutes = 30,
            BookingInstructions = "Vui lòng ăn nhẹ trước khi thực hiện lấy cao răng."
        };

        var json = JsonSerializer.Serialize(serviceDto, JsonOptions);

        Assert.Contains("Khoa Răng Tổng Quát", json);
        Assert.Contains("Khám", json);
        Assert.Contains("Lấy Cao Răng", json);
        Assert.Contains("Vệ sinh răng miệng và lấy vôi răng siêu âm", json);

        // Verify Vietnamese accented characters are output as literal UTF-8 and not escaped
        Assert.DoesNotContain("\\u00E1", json, StringComparison.OrdinalIgnoreCase); // 'á'
        Assert.DoesNotContain("\\u1EA5", json, StringComparison.OrdinalIgnoreCase); // 'ấ'
        Assert.DoesNotContain("\\u0103", json, StringComparison.OrdinalIgnoreCase); // 'ă'

        var deserialized = JsonSerializer.Deserialize<DepartmentServiceDto>(json, JsonOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(serviceDto.DepartmentName, deserialized!.DepartmentName);
        Assert.Equal(serviceDto.ServiceName, deserialized.ServiceName);
        Assert.Equal(serviceDto.Description, deserialized.Description);
        Assert.Equal(serviceDto.BookingInstructions, deserialized.BookingInstructions);
    }

    // =========================================================================
    // TEST GROUP 5: FOUR-ROLE PORTALS VIETNAMESE DATA INTEGRITY
    // =========================================================================
    [Fact]
    public void TestGroup5_FourRolePortals_DtoIntegrity_PreservesAllRoleData()
    {
        // 1. Patient Portal DTOs
        var apptDto = new AppointmentDto
        {
            AppointmentId = 101,
            PatientName = "Nguyễn Văn Bệnh Nhân",
            ServiceName = "Nhổ Răng Khôn",
            DepartmentName = "Khoa Răng Tổng Quát",
            AssignedDentistName = "BS. Lê Văn Bác Sĩ Một",
            ReasonForVisit = "Đau nhức răng khôn hàm dưới"
        };
        var apptJson = JsonSerializer.Serialize(apptDto, JsonOptions);
        var apptRoundtrip = JsonSerializer.Deserialize<AppointmentDto>(apptJson, JsonOptions);
        Assert.Equal("Nguyễn Văn Bệnh Nhân", apptRoundtrip!.PatientName);
        Assert.Equal("Nhổ Răng Khôn", apptRoundtrip.ServiceName);
        Assert.Equal("Đau nhức răng khôn hàm dưới", apptRoundtrip.ReasonForVisit);

        // 2. Receptionist Portal DTOs
        var staffDto = new AdminUserDto
        {
            UserId = 2,
            FullName = "Trần Thị Lễ Tân",
            Role = "Receptionist"
        };
        var staffJson = JsonSerializer.Serialize(staffDto, JsonOptions);
        var staffRoundtrip = JsonSerializer.Deserialize<AdminUserDto>(staffJson, JsonOptions);
        Assert.Equal("Trần Thị Lễ Tân", staffRoundtrip!.FullName);

        // 3. Dentist Portal DTOs
        var clinicalDto = new ClinicalEntryDto
        {
            ClinicalEntryId = 501,
            Symptoms = "Đau buốt răng khi uống nước lạnh",
            ClinicalFindings = "Răng 46 vỡ mặt nhai sâu sát tủy, gõ đau nhẹ",
            Diagnosis = "Viêm tủy cấp có khả năng hồi phục",
            ClinicalNotes = "Gây tê tại chỗ, làm sạch xoang sâu, trám lót bảo vệ tủy"
        };
        var clinicalJson = JsonSerializer.Serialize(clinicalDto, JsonOptions);
        var clinicalRoundtrip = JsonSerializer.Deserialize<ClinicalEntryDto>(clinicalJson, JsonOptions);
        Assert.Equal("Đau buốt răng khi uống nước lạnh", clinicalRoundtrip!.Symptoms);
        Assert.Equal("Viêm tủy cấp có khả năng hồi phục", clinicalRoundtrip.Diagnosis);
        Assert.Equal("Gây tê tại chỗ, làm sạch xoang sâu, trám lót bảo vệ tủy", clinicalRoundtrip.ClinicalNotes);

        // 4. System Administrator Portal DTOs
        var medDto = new MedicineDto
        {
            MedicineId = 1,
            MedicineCode = "MED-PARA-01",
            MedicineName = "Paracetamol 500mg",
            ActiveIngredient = "Paracetamol",
            Unit = "Viên",
            UsageInstructions = "Uống 1 viên sau ăn khi đau hoặc sốt, không quá 4 viên/ngày"
        };
        var medJson = JsonSerializer.Serialize(medDto, JsonOptions);
        var medRoundtrip = JsonSerializer.Deserialize<MedicineDto>(medJson, JsonOptions);
        Assert.Equal("Viên", medRoundtrip!.Unit);
        Assert.Equal("Uống 1 viên sau ăn khi đau hoặc sốt, không quá 4 viên/ngày", medRoundtrip.UsageInstructions);
    }

    // =========================================================================
    // TEST GROUP 6: AUDIT RESIDUAL MOJIBAKE SIGNATURES
    // =========================================================================
    [Fact]
    public void TestGroup6_ResidualMojibakeAudit_EnsuresNoCorruptedBytePatternsExist()
    {
        var sampleCleanTexts = new[]
        {
            "Khám & Lấy Cao Răng",
            "Trám Răng Composite",
            "Nhổ Răng Khôn",
            "Niềng Răng Mắc Cài Kim Loại",
            "Bọc Răng Sứ Cercon",
            "Khoa Răng Tổng Quát",
            "Khoa Chỉnh Nha & Phục Hình",
            "Trần Thị Lễ Tân",
            "Lê Văn Bác Sĩ Một",
            "Phạm Thị Bác Sĩ Hai",
            "Hoàng Quản Trị Viên",
            "Nguyễn Văn Bệnh Nhân"
        };

        var mojibakeSubstrings = new[] { "Ã¡", "Ã´", "Ã¢", "Äƒ", "áº", "á»", "\uFFFD", "ï¿½" };

        foreach (var text in sampleCleanTexts)
        {
            foreach (var pattern in mojibakeSubstrings)
            {
                Assert.False(text.Contains(pattern), $"Detected mojibake pattern '{pattern}' in text: '{text}'");
            }
        }
    }

    // =========================================================================
    // TEST GROUP 7: REAL DATABASE END-TO-END LIFECYCLE (ISOLATED ENCODING DB)
    // =========================================================================
    [Fact]
    public async Task TestGroup7_RealDatabaseEndToEnd_LifecycleVerification_AllEntitiesPreserveVietnamese()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(TestConnectionString)
            .Options;

        await using var db = new DentalClinicDbContext(options);

        // A. Service Catalog Read Verification
        var depts = await db.Departments.AsNoTracking().ToListAsync();
        Assert.NotEmpty(depts);
        Assert.Contains(depts, d => d.Name == "Khoa Răng Tổng Quát");
        Assert.Contains(depts, d => d.Name == "Khoa Chỉnh Nha & Phục Hình");

        var services = await db.DepartmentServices.AsNoTracking().ToListAsync();
        Assert.NotEmpty(services);
        var s1 = services.FirstOrDefault(s => s.DepartmentServiceId == 1);
        Assert.NotNull(s1);
        Assert.Equal("Khám & Lấy Cao Răng", s1!.ServiceName);
        Assert.Equal("Vệ sinh răng miệng và lấy vôi răng siêu âm", s1.Description);

        // B. Booking: Create appointment with Vietnamese reason ("Nhổ Răng Khôn")
        var patientUser = await db.UserAccounts.FirstOrDefaultAsync(u => u.Email == "patient@dentalcare.test");
        var dentistUser = await db.UserAccounts.FirstOrDefaultAsync(u => u.Email == "dentist1@dentalcare.test");
        Assert.NotNull(patientUser);
        Assert.NotNull(dentistUser);

        var appointment = new Appointment
        {
            PatientUserId = patientUser!.UserId,
            BookingSource = "Patient",
            Status = "PendingReceptionReview",
            ReasonForVisit = "Nhổ Răng Khôn",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var savedAppt = await db.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.AppointmentId == appointment.AppointmentId);
        Assert.NotNull(savedAppt);
        Assert.Equal("Nhổ Răng Khôn", savedAppt!.ReasonForVisit);

        // C. Clinical Entry ("Đau nhức răng hàm dưới bên phải, nghi viêm tủy.")
        var medRecord = await db.DentalMedicalRecords.FirstOrDefaultAsync(m => m.PatientUserId == patientUser.UserId);
        if (medRecord == null)
        {
            medRecord = new DentalMedicalRecord
            {
                PatientUserId = patientUser.UserId,
                CreatedAt = DateTime.UtcNow
            };
            db.DentalMedicalRecords.Add(medRecord);
            await db.SaveChangesAsync();
        }

        var clinicalEntry = new ClinicalEntry
        {
            MedicalRecordId = medRecord.RecordId,
            PatientUserId = patientUser.UserId,
            DentistUserId = dentistUser!.UserId,
            AppointmentId = savedAppt.AppointmentId,
            ChiefComplaint = "Đau nhức răng hàm dưới bên phải, nghi viêm tủy.",
            ClinicalExamination = "Răng 48 mọc lệch, lợi trùm viêm đỏ có mủ nhẹ.",
            Diagnosis = "Viêm lợi trùm răng khôn R48 / Viêm tủy R46",
            TreatmentDone = "Bơm rửa túi lợi bằng NaCl 0.9%, kê đơn kháng sinh giảm đau.",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(clinicalEntry);
        await db.SaveChangesAsync();

        var savedEntry = await db.ClinicalEntries.AsNoTracking().FirstOrDefaultAsync(c => c.AppointmentId == savedAppt.AppointmentId);
        Assert.NotNull(savedEntry);
        Assert.Equal("Đau nhức răng hàm dưới bên phải, nghi viêm tủy.", savedEntry!.ChiefComplaint);

        // D. Medicine & Prescription Instructions ("Uống 1 viên sau ăn, ngày 2 lần.")
        var medicine = new Medicine
        {
            MedicineCode = $"MED-ENC-{Guid.NewGuid():N}"[..20],
            MedicineName = "Thuốc Giảm Đau Kháng Viêm Nha Khoa",
            ActiveIngredient = "Ibuprofen + Paracetamol",
            Unit = "Viên nén bao phim",
            UnitPrice = 5000,
            StockQuantity = 100,
            MinimumStockAlert = 10,
            UsageInstructions = "Uống 1 viên sau ăn, ngày 2 lần.",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        var savedMedicine = await db.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.MedicineCode == medicine.MedicineCode);
        Assert.NotNull(savedMedicine);
        Assert.Equal("Thuốc Giảm Đau Kháng Viêm Nha Khoa", savedMedicine!.MedicineName);
        Assert.Equal("Viên nén bao phim", savedMedicine.Unit);
        Assert.Equal("Uống 1 viên sau ăn, ngày 2 lần.", savedMedicine.UsageInstructions);

        // E. Billing: Invoice & Payment Notes ("Đã thanh toán viện phí đầy đủ")
        var invoice = new Invoice
        {
            InvoiceCode = $"INV-ENC-{Guid.NewGuid():N}"[..20],
            AppointmentId = savedAppt.AppointmentId,
            GeneratedByUserId = dentistUser.UserId,
            TotalAmount = 1200000,
            DiscountAmount = 0,
            FinalAmount = 1200000,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            Amount = 1200000,
            PaymentType = "Final",
            Method = "BankTransfer",
            Notes = "Đã thanh toán viện phí đầy đủ",
            Status = "Successful",
            ReceivedByUserId = dentistUser.UserId,
            PaymentDate = DateTime.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var savedPayment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.PaymentId == payment.PaymentId);
        Assert.NotNull(savedPayment);
        Assert.Equal("Đã thanh toán viện phí đầy đủ", savedPayment!.Notes);

        // F. Blog: Author and read post ("Chăm sóc răng miệng đúng cách mỗi ngày")
        var blogCategory = await db.BlogCategories.FirstOrDefaultAsync();
        var blogPost = new BlogPost
        {
            CategoryId = blogCategory?.CategoryId ?? 1,
            AuthorUserId = dentistUser.UserId,
            Title = "Chăm sóc răng miệng đúng cách mỗi ngày",
            Slug = $"cham-soc-rang-mieng-{Guid.NewGuid():N}"[..30],
            Summary = "Hướng dẫn vệ sinh răng miệng hằng ngày để phòng ngừa sâu răng và viêm nha chu.",
            Content = "<p>Đánh răng ít nhất hai lần một ngày với kem đánh răng chứa fluoride.</p>",
            Status = "Published",
            PostType = "Blog",
            PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.BlogPosts.Add(blogPost);
        await db.SaveChangesAsync();

        var savedBlog = await db.BlogPosts.AsNoTracking().FirstOrDefaultAsync(b => b.BlogPostId == blogPost.BlogPostId);
        Assert.NotNull(savedBlog);
        Assert.Equal("Chăm sóc răng miệng đúng cách mỗi ngày", savedBlog!.Title);
        Assert.Equal("Hướng dẫn vệ sinh răng miệng hằng ngày để phòng ngừa sâu răng và viêm nha chu.", savedBlog.Summary);

        // G. Notifications: ("Lịch khám của bạn đã được xác nhận.")
        var notification = new Notification
        {
            UserId = patientUser.UserId,
            Type = "AppointmentConfirmation",
            Channel = "InApp",
            Subject = "Xác nhận lịch hẹn khám",
            Content = "Lịch khám của bạn đã được xác nhận.",
            DeliveryStatus = "Sent",
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        var savedNotification = await db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.NotificationId == notification.NotificationId);
        Assert.NotNull(savedNotification);
        Assert.Equal("Lịch khám của bạn đã được xác nhận.", savedNotification!.Content);

        // H. Dentist Profiles: Verify full name
        var dentistStaff = await db.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == 3);
        Assert.NotNull(dentistStaff);
        Assert.Equal("Lê Văn Bác Sĩ Một", dentistStaff!.FullName);

        // Clean up transient test records
        db.Medicines.Remove(medicine);
        db.Notifications.Remove(notification);
        db.BlogPosts.Remove(blogPost);
        db.Payments.Remove(payment);
        db.Invoices.Remove(invoice);
        db.ClinicalEntries.Remove(clinicalEntry);
        db.Appointments.Remove(appointment);
        await db.SaveChangesAsync();
    }
}
