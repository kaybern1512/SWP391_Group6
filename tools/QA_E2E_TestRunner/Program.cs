using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace QA_E2E_TestRunner;

/// <summary>
/// SWP391_GROUP6 - QA Automated E2E & Concurrency Integrity Test Runner.
/// STRICTLY executes against isolated QA database (DentalClinic_QA_E2E).
/// PRODUCTION DATABASE (DentalClinicManagementDB) IS HARD-GUARDED AND PROTECTED.
/// </summary>
public class Program
{
    private const string BaseUrl = "https://localhost:7129";
    private const string ApiBaseUrl = "https://localhost:7350";
    private const string AllowedQaDatabase = "DentalClinic_QA_E2E";
    private const string ConnectionString = "Server=.\\SQL2025_DUC;Database=DentalClinic_QA_E2E;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=false;";
    private static readonly string ScreenshotDir = @"D:\SWP\SWP391_Group6\reports\screenshots";
    private static readonly ConcurrentDictionary<string, string> TokenCache = new();

    public static string GetQaPassword()
    {
        var password = Environment.GetEnvironmentVariable("QA_TEST_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("[SECURITY FAIL-FAST] Missing or empty required environment variable 'QA_TEST_PASSWORD'. All QA execution halted before any database operations.");
        }
        return password;
    }

    public static void EnsureDatabaseGuard(SqlConnection conn)
    {
        var db = conn.Database;
        if (string.Equals(db, "DentalClinicManagementDB", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"[SECURITY ALERT] Operation blocked: Attempted execution on PRODUCTION DATABASE '{db}'.");
        }

        if (!string.Equals(db, AllowedQaDatabase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"[DATABASE GUARD] Unauthorized database '{db}'. All QA actions are strictly restricted to '{AllowedQaDatabase}'.");
        }
    }

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("================================================================================");
        Console.WriteLine("        SWP391_GROUP6 - HARDENED QA E2E & CONCURRENCY RUNNER (v2.2)             ");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"[CONFIG] Web Base URL      : {BaseUrl}");
        Console.WriteLine($"[CONFIG] API Base URL      : {ApiBaseUrl}");
        Console.WriteLine($"[CONFIG] Target QA Database : {AllowedQaDatabase}");
        Console.WriteLine($"[CONFIG] Screenshots Dir   : {ScreenshotDir}");
        Console.WriteLine($"[SECURITY] Password Source : Environment Variable 'QA_TEST_PASSWORD'");

        // 0. Fail-fast credential validation: abort before any database connection or mutation
        string qaPassword;
        try
        {
            qaPassword = GetQaPassword();
            Console.WriteLine("[SECURITY] Environment variable 'QA_TEST_PASSWORD' is present and verified.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n{ex.Message}");
            Console.ResetColor();
            Console.WriteLine("[SAFETY GUARD] No database connection was opened. No database cleanup or mutation was performed.\n");
            return 2;
        }

        Directory.CreateDirectory(ScreenshotDir);

        // 1. Safety Guard Check on Database Connection
        await using (var guardConn = new SqlConnection(ConnectionString))
        {
            await guardConn.OpenAsync();
            EnsureDatabaseGuard(guardConn);
            Console.WriteLine($"[GUARD] Verified database target: '{guardConn.Database}' is authorized for QA operations.");
        }

        // 2. Preflight database hygiene: clean previous test records safely
        await CleanDatabaseBeforeTestAsync();
        Console.WriteLine("[INIT] Cleaned prior test records from isolated QA database.");

        // 3. Locate browser executable
        var edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        if (!File.Exists(edgePath))
        {
            edgePath = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        }

        var tempProfile = Path.Combine(Path.GetTempPath(), $"edge_qa_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempProfile);

        var edgeProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = edgePath,
                Arguments = $"--headless=new --remote-debugging-port=9222 --ignore-certificate-errors --user-data-dir=\"{tempProfile}\" --disable-gpu --disable-extensions --no-first-run --no-default-browser-check about:blank",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            Console.WriteLine("[BROWSER] Launching headless browser with CDP debugging port 9222...");
            edgeProcess.Start();
            await Task.Delay(2500);

            using var http = new HttpClient();
            var versionJson = await http.GetStringAsync("http://localhost:9222/json/version");
            using var versionDoc = JsonDocument.Parse(versionJson);
            Console.WriteLine($"[BROWSER] Connected to CDP: {versionDoc.RootElement.GetProperty("Browser").GetString()}");

            var tabsJson = await http.GetStringAsync("http://localhost:9222/json/list");
            using var tabsDoc = JsonDocument.Parse(tabsJson);
            string? pageWs = null;
            foreach (var tab in tabsDoc.RootElement.EnumerateArray())
            {
                if (tab.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "page")
                {
                    pageWs = tab.GetProperty("webSocketDebuggerUrl").GetString();
                    break;
                }
            }
            if (string.IsNullOrEmpty(pageWs))
            {
                pageWs = tabsDoc.RootElement[0].GetProperty("webSocketDebuggerUrl").GetString()!;
            }

            using var cdp = new CdpClient(pageWs);
            await cdp.ConnectAsync();
            await cdp.SendAsync("Page.enable");
            await cdp.SendAsync("Runtime.enable");
            await cdp.SendAsync("DOM.enable");
            await cdp.SendAsync("Network.enable");
            await cdp.SendAsync("Emulation.setDeviceMetricsOverride", new
            {
                width = 1440,
                height = 900,
                deviceScaleFactor = 1,
                mobile = false
            });

            Console.WriteLine("[BROWSER] CDP Session successfully initialized!\n");

            var results = new List<TestStepResult>();

            // ========================================================================================
            // JOURNEY STEP 1: PATIENT LOGIN & DASHBOARD [BROWSER_DOM_TEST]
            // ========================================================================================
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("[STEP 1] Testing Patient Login & Dashboard [BROWSER_DOM_TEST]...");
            await ClearCookiesAndNavigateLoginAsync(cdp);
            await VerifyPageContentAsync(cdp, "/Account/Login", new[] { "Đăng Nhập", "Email" }, "Login");
            await CaptureVerifiedScreenshotAsync(cdp, "01_login_page.png");

            await SubmitLoginFormAsync(cdp, "lehoangminh.test@dentalcare.com", qaPassword);
            await Task.Delay(3000);

            await VerifyPageContentAsync(cdp, "/Patient", new[] { "Lê Hoàng Minh", "Cổng Thông Tin Bệnh Nhân", "Patient" }, "Patient Dashboard");
            await CaptureVerifiedScreenshotAsync(cdp, "02_patient_dashboard.png");

            var patientUrl = await cdp.EvaluateAsync<string>("window.location.href") ?? "";
            results.Add(new TestStepResult("1.1", "Patient Login & Authentication", "BROWSER_DOM_TEST", true, $"Verified URL: {patientUrl}"));
            Console.WriteLine($"  -> Result: PASS (Patient Dashboard Verified)");

            // ========================================================================================
            // JOURNEY STEP 2: PATIENT BOOKING APPOINTMENT [DATABASE_VERIFICATION & BROWSER_VIEW]
            // ========================================================================================
            Console.WriteLine("\n[STEP 2] Testing Patient Appointment Booking [DATABASE_VERIFICATION & BROWSER_VIEW]...");
            await cdp.NavigateAsync($"{BaseUrl}/Patient/Appointment/Book");
            await Task.Delay(1500);
            await VerifyPageContentAsync(cdp, "/Patient/Appointment/Book", new[] { "Đặt Lịch", "Chuyên Khoa", "Khám" }, "Booking Form");
            await CaptureVerifiedScreenshotAsync(cdp, "03_patient_booking_form.png");

            var targetDate = DateTime.Today.AddDays(3);
            var slotStart = targetDate.AddHours(9);
            var slotEnd = slotStart.AddMinutes(30);

            await EnsureDoctorShiftAsync(4, 3, targetDate, "08:00:00", "17:00:00");

            var apptCode = $"APT-E2E-{Guid.NewGuid():N}"[..18];
            long apptId;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Appointments (
                        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
                        ScheduledStart, ScheduledEnd, Status, BookingSource, ReasonForVisit, CreatedAt, UpdatedAt
                    ) VALUES (
                        @code, 6, 3, 7, @start, @end, 'PendingReceptionReview', 'Patient', N'Khám phục hình răng thẩm mỹ E2E', SYSUTCDATETIME(), SYSUTCDATETIME()
                    );
                    SELECT SCOPE_IDENTITY();
                ";
                cmd.Parameters.AddWithValue("@code", apptCode);
                cmd.Parameters.AddWithValue("@start", slotStart);
                cmd.Parameters.AddWithValue("@end", slotEnd);
                apptId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            await cdp.NavigateAsync($"{BaseUrl}/Patient/Appointment/Detail/{apptId}");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, $"/Patient/Appointment/Detail/{apptId}", new[] { apptCode, "Chờ Lễ Tân", "PendingReceptionReview" }, "Patient Appointment Detail");
            await CaptureVerifiedScreenshotAsync(cdp, "04_patient_appointment_detail_pending.png");

            results.Add(new TestStepResult("1.2", "Patient Book Appointment (Status = PendingReceptionReview)", "DATABASE_VERIFICATION", true, $"Created ApptId: {apptId}, Code: {apptCode}"));
            Console.WriteLine($"  -> Result: PASS (Code: {apptCode})");

            // ========================================================================================
            // JOURNEY STEP 3: RECEPTIONIST LOGIN & FORWARD TO MANAGER [API_INTEGRATION & BROWSER_VIEW]
            // ========================================================================================
            Console.WriteLine("\n[STEP 3] Testing Receptionist Login & Requests Queue [API_INTEGRATION & BROWSER_VIEW]...");
            await ClearCookiesAndNavigateLoginAsync(cdp);
            await SubmitLoginFormAsync(cdp, "receptionist@dentalcare.com", qaPassword);
            await Task.Delay(3000);

            await VerifyPageContentAsync(cdp, "/Receptionist", new[] { "Bàn Tiếp Đón", "Lễ Tân", "Receptionist" }, "Receptionist Dashboard");
            await CaptureVerifiedScreenshotAsync(cdp, "05_receptionist_dashboard.png");

            await cdp.NavigateAsync($"{BaseUrl}/Receptionist/Appointments/Requests");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, "/Receptionist/Appointments/Requests", new[] { "Hàng Đợi Tiếp Nhận", "Tiếp nhận yêu cầu", "Lễ Tân" }, "Receptionist Requests Queue");
            await CaptureVerifiedScreenshotAsync(cdp, "06_receptionist_requests_queue.png");

            var fwdRes = await PostAsync($"{ApiBaseUrl}/api/receptionist/appointments/{apptId}/forward", new
            {
                note = "Lễ tân xác nhận thông tin đầy đủ, chuyển Trưởng khoa duyệt"
            }, "receptionist@dentalcare.com");

            string? fwdStatus = null;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT Status FROM Appointments WHERE AppointmentId = @id";
                checkCmd.Parameters.AddWithValue("@id", apptId);
                fwdStatus = (string?)await checkCmd.ExecuteScalarAsync();
            }

            bool forwardOk = fwdRes.IsSuccess && fwdStatus == "PendingDepartmentReview";
            results.Add(new TestStepResult("1.3", "Receptionist Forward to Manager (Status = PendingDepartmentReview)", "API_INTEGRATION", forwardOk, $"ApptId: {apptId} status: {fwdStatus}"));
            Console.WriteLine($"  -> Result: {(forwardOk ? "PASS" : "FAIL")}");

            // ========================================================================================
            // JOURNEY STEP 4: DEPARTMENT MANAGER CONFIRM & RESOURCE ALLOCATION [API_INTEGRATION & BROWSER_VIEW]
            // ========================================================================================
            Console.WriteLine("\n[STEP 4] Testing Department Manager Login & Queue [API_INTEGRATION & BROWSER_VIEW]...");
            await ClearCookiesAndNavigateLoginAsync(cdp);
            await SubmitLoginFormAsync(cdp, "manager@dentalcare.com", qaPassword);
            await Task.Delay(3000);

            await VerifyPageContentAsync(cdp, "/Department", new[] { "Bàn Quản Lý Chuyên Khoa", "DepartmentManager", "Trưởng khoa" }, "Department Manager Dashboard");
            await CaptureVerifiedScreenshotAsync(cdp, "07_manager_dashboard.png");

            await cdp.NavigateAsync($"{BaseUrl}/Department/Appointments/Requests");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, "/Department/Appointments/Requests", new[] { "Hàng Đợi Phân Bổ", "Trưởng khoa", "Chuyên khoa" }, "Department Manager Requests Queue");
            await CaptureVerifiedScreenshotAsync(cdp, "08_manager_requests_queue.png");

            var confirmRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{apptId}/confirm", new
            {
                assignedDentistUserId = 4,
                roomId = 2,
                dentalChairId = 3,
                scheduledStart = slotStart,
                scheduledEnd = slotEnd
            }, "manager@dentalcare.com");

            string? finalQueueNum = null;
            string? confirmedStatus = null;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT Status, QueueNumber, RoomId, DentalChairId, AssignedDentistUserId FROM Appointments WHERE AppointmentId = @id";
                checkCmd.Parameters.AddWithValue("@id", apptId);
                await using var r = await checkCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    confirmedStatus = r.IsDBNull(0) ? null : r.GetString(0);
                    finalQueueNum = r.IsDBNull(1) ? null : r.GetString(1);
                }
            }

            bool managerConfirmOk = confirmRes.IsSuccess && confirmedStatus == "Confirmed" && !string.IsNullOrEmpty(finalQueueNum);

            // Re-authenticate as Patient to inspect the confirmed detail page legally
            await ClearCookiesAndNavigateLoginAsync(cdp);
            await SubmitLoginFormAsync(cdp, "lehoangminh.test@dentalcare.com", qaPassword);
            await Task.Delay(2500);

            await cdp.NavigateAsync($"{BaseUrl}/Patient/Appointment/Detail/{apptId}");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, $"/Patient/Appointment/Detail/{apptId}", new[] { "Confirmed", "Đã Xác Nhận", finalQueueNum ?? "Q001", apptCode }, "Confirmed Appointment Detail");
            await CaptureVerifiedScreenshotAsync(cdp, "09_appointment_confirmed_detail.png");

            results.Add(new TestStepResult("1.4", "Manager Confirm & Assign Room/Chair (Status = Confirmed)", "API_INTEGRATION", managerConfirmOk, $"Queue: {finalQueueNum}, Status: {confirmedStatus}"));
            Console.WriteLine($"  -> Result: {(managerConfirmOk ? "PASS" : "FAIL")} (Queue: {finalQueueNum})");

            // ========================================================================================
            // JOURNEY STEP 5: PATIENT RESCHEDULE [API_INTEGRATION & BROWSER_VIEW]
            // ========================================================================================
            Console.WriteLine("\n[STEP 5] Testing Patient Reschedule [API_INTEGRATION & BROWSER_VIEW]...");
            var newDate = targetDate.AddDays(1);
            var newStart = newDate.AddHours(14);
            var newEnd = newStart.AddMinutes(30);
            await EnsureDoctorShiftAsync(4, 3, newDate, "08:00:00", "17:00:00");

            var reschedRes = await PostAsync($"{ApiBaseUrl}/api/patient/appointments/{apptId}/reschedule", new
            {
                newScheduledStart = newStart,
                reason = "Bệnh nhân có lịch bận đột xuất, xin dời sang ngày hôm sau"
            }, "lehoangminh.test@dentalcare.com");

            long newApptId = 0;
            string? oldApptStatus = null;
            string? newApptStatus = null;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Status FROM Appointments WHERE AppointmentId = @id";
                cmd.Parameters.AddWithValue("@id", apptId);
                oldApptStatus = (string?)await cmd.ExecuteScalarAsync();

                await using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "SELECT TOP 1 AppointmentId, Status FROM Appointments WHERE PatientUserId = 6 AND AppointmentId != @id ORDER BY AppointmentId DESC";
                cmd2.Parameters.AddWithValue("@id", apptId);
                await using var r2 = await cmd2.ExecuteReaderAsync();
                if (await r2.ReadAsync())
                {
                    newApptId = r2.GetInt64(0);
                    newApptStatus = r2.GetString(1);
                }
            }

            bool reschedOk = reschedRes.IsSuccess && oldApptStatus == "Cancelled" && newApptStatus == "PendingReceptionReview";

            await cdp.NavigateAsync($"{BaseUrl}/Patient/Appointment/Detail/{newApptId}");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, $"/Patient/Appointment/Detail/{newApptId}", new[] { "PendingReceptionReview", "Chờ Lễ Tân", "Chi tiết" }, "Rescheduled New Appointment Detail");
            await CaptureVerifiedScreenshotAsync(cdp, "10_patient_rescheduled_new_appointment.png");

            results.Add(new TestStepResult("1.5", "Patient Reschedule (Old = Cancelled, New = PendingReceptionReview)", "API_INTEGRATION", reschedOk, $"OldApptId: {apptId} (Cancelled), NewApptId: {newApptId} ({newApptStatus})"));
            Console.WriteLine($"  -> Result: {(reschedOk ? "PASS" : "FAIL")}");

            // ========================================================================================
            // JOURNEY STEP 6: CANCELLATION (STAFF CANCEL) [API_INTEGRATION]
            // ========================================================================================
            Console.WriteLine("\n[STEP 6] Testing Appointment Cancellation [API_INTEGRATION]...");
            var cancelRes = await PostAsync($"{ApiBaseUrl}/api/receptionist/appointments/{newApptId}/cancel", new
            {
                reason = "Lễ tân hỗ trợ hủy lịch theo cuộc gọi của bệnh nhân"
            }, "receptionist@dentalcare.com");

            string? cancelledStatus = null;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Status FROM Appointments WHERE AppointmentId = @id";
                cmd.Parameters.AddWithValue("@id", newApptId);
                cancelledStatus = (string?)await cmd.ExecuteScalarAsync();
            }

            bool cancelOk = cancelRes.IsSuccess && cancelledStatus == "Cancelled";
            results.Add(new TestStepResult("1.6", "Appointment Staff Cancellation (Status = Cancelled)", "API_INTEGRATION", cancelOk, $"NewApptId: {newApptId} cancelled cleanly"));
            Console.WriteLine($"  -> Result: {(cancelOk ? "PASS" : "FAIL")}");

            // ========================================================================================
            // JOURNEY STEP 7 & 8: REBOOK & RECEPTIONIST CHECK-IN [API_INTEGRATION & BROWSER_VIEW]
            // ========================================================================================
            Console.WriteLine("\n[STEP 7 & 8] Rebooking & Testing Receptionist Check-In [API_INTEGRATION & BROWSER_VIEW]...");
            var todayDate = DateTime.Today;
            var todayStart = todayDate.AddHours(10);
            var todayEnd = todayStart.AddMinutes(30);
            await EnsureDoctorShiftAsync(4, 3, todayDate, "08:00:00", "17:00:00");

            var rebookCode = $"APT-E2E-TODAY-{Guid.NewGuid():N}"[..18];
            long rebookId;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Appointments (
                        AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
                        ScheduledStart, ScheduledEnd, Status, BookingSource, ReasonForVisit, CreatedAt, UpdatedAt
                    ) VALUES (
                        @code, 6, 3, 7, @start, @end, 'PendingReceptionReview', 'Patient', N'Tái khám điều trị tủy E2E', SYSUTCDATETIME(), SYSUTCDATETIME()
                    );
                    SELECT SCOPE_IDENTITY();
                ";
                cmd.Parameters.AddWithValue("@code", rebookCode);
                cmd.Parameters.AddWithValue("@start", todayStart);
                cmd.Parameters.AddWithValue("@end", todayEnd);
                rebookId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            await PostAsync($"{ApiBaseUrl}/api/receptionist/appointments/{rebookId}/forward", new { note = "Hồ sơ chuẩn bị sẵn sàng" }, "receptionist@dentalcare.com");
            await PostAsync($"{ApiBaseUrl}/api/department/appointments/{rebookId}/confirm", new
            {
                assignedDentistUserId = 4,
                roomId = 2,
                dentalChairId = 3,
                scheduledStart = todayStart,
                scheduledEnd = todayEnd
            }, "manager@dentalcare.com");

            await ClearCookiesAndNavigateLoginAsync(cdp);
            await SubmitLoginFormAsync(cdp, "receptionist@dentalcare.com", qaPassword);
            await Task.Delay(2500);

            await cdp.NavigateAsync($"{BaseUrl}/Receptionist/Appointments/TodayCheckIn");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, "/Receptionist/Appointments/TodayCheckIn", new[] { "Check-in", "Bệnh Nhân", "Hôm Nay" }, "Receptionist Today Check-in");
            await CaptureVerifiedScreenshotAsync(cdp, "11_receptionist_today_checkin_list.png");

            var checkInRes = await PostAsync($"{ApiBaseUrl}/api/receptionist/appointments/{rebookId}/check-in", new
            {
                note = "Bệnh nhân đã đến phòng khám đúng giờ"
            }, "receptionist@dentalcare.com");

            string? checkedInStatus = null;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Status FROM Appointments WHERE AppointmentId = @id";
                cmd.Parameters.AddWithValue("@id", rebookId);
                checkedInStatus = (string?)await cmd.ExecuteScalarAsync();
            }

            bool checkInOk = checkInRes.IsSuccess && checkedInStatus == "CheckedIn";

            await cdp.NavigateAsync($"{BaseUrl}/Receptionist/Appointments/TodayCheckIn");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, "/Receptionist/Appointments/TodayCheckIn", new[] { "Check-in", "Bệnh Nhân", "Hôm Nay" }, "Receptionist CheckedIn View");
            await CaptureVerifiedScreenshotAsync(cdp, "12_receptionist_checkedin_success.png");

            results.Add(new TestStepResult("1.7", "Receptionist Patient Check-In (Status = CheckedIn)", "API_INTEGRATION", checkInOk, $"RebookId: {rebookId} status = {checkedInStatus}"));
            Console.WriteLine($"  -> Result: {(checkInOk ? "PASS" : "FAIL")}");

            // ========================================================================================
            // JOURNEY STEP 9: DENTIST ASSIGN CHAIR & INSERVICE [API_INTEGRATION & BROWSER_VIEW]
            // ========================================================================================
            Console.WriteLine("\n[STEP 9] Testing Dentist Login, Chair Selection & InService [API_INTEGRATION & BROWSER_VIEW]...");
            await ClearCookiesAndNavigateLoginAsync(cdp);
            await SubmitLoginFormAsync(cdp, "dentist@dentalcare.com", qaPassword);
            await Task.Delay(3000);

            await VerifyPageContentAsync(cdp, "/Dentist", new[] { "Không Gian Lâm Sàng Bác Sĩ", "Dentist", "Bác Sĩ" }, "Dentist Dashboard");
            await CaptureVerifiedScreenshotAsync(cdp, "13_dentist_dashboard.png");

            await cdp.NavigateAsync($"{BaseUrl}/Dentist/Appointments/Today");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, "/Dentist/Appointments/Today", new[] { "Lịch Thăm Khám", "Hôm Nay", "Ngày khám" }, "Dentist Today Schedule");
            await CaptureVerifiedScreenshotAsync(cdp, "14_dentist_today_schedule.png");

            var assignChairRes = await PostAsync($"{ApiBaseUrl}/api/dentist/appointments/{rebookId}/assign-chair", new
            {
                dentalChairId = 3
            }, "dentist@dentalcare.com");

            string? inServiceStatus = null;
            long? assignedChair = null;
            await using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                EnsureDatabaseGuard(conn);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Status, DentalChairId FROM Appointments WHERE AppointmentId = @id";
                cmd.Parameters.AddWithValue("@id", rebookId);
                await using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    inServiceStatus = r.GetString(0);
                    assignedChair = r.IsDBNull(1) ? null : r.GetInt64(1);
                }
            }

            bool inServiceOk = assignChairRes.IsSuccess && inServiceStatus == "InService" && assignedChair == 3;

            await cdp.NavigateAsync($"{BaseUrl}/Dentist/Appointments/Today");
            await Task.Delay(2000);
            await VerifyPageContentAsync(cdp, "/Dentist/Appointments/Today", new[] { "Lịch Thăm Khám", "Hôm Nay" }, "Dentist Active InService View");
            await CaptureVerifiedScreenshotAsync(cdp, "15_dentist_inservice_active.png");

            results.Add(new TestStepResult("1.8", "Dentist Assign Chair & Start (Status = InService)", "API_INTEGRATION", inServiceOk, $"RebookId: {rebookId}, Chair: {assignedChair}, Status: {inServiceStatus}"));
            Console.WriteLine($"  -> Result: {(inServiceOk ? "PASS" : "FAIL")}");

            // ========================================================================================
            // CONCURRENCY & INTEGRITY HARDENING EDGE-CASE VERIFICATION [DATABASE_VERIFICATION & API]
            // ========================================================================================
            Console.WriteLine("\n================================================================================");
            Console.WriteLine("    CONCURRENCY & INTEGRITY HARDENING EDGE CASES [DATABASE_VERIFICATION & API]  ");
            Console.WriteLine("================================================================================");

            // 4.1: Doctor Overlapping Conflict Guard
            Console.WriteLine("[TEST 4.1] Doctor Overlapping Conflict Rejection...");
            var docConflictAppt = await CreatePendingApptAsync(52, 3, todayStart, todayEnd);
            var docConflictRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{docConflictAppt}/confirm", new
            {
                assignedDentistUserId = 4,
                roomId = 2,
                dentalChairId = 4,
                scheduledStart = todayStart,
                scheduledEnd = todayEnd
            }, "manager@dentalcare.com");
            bool docConflictBlocked = !docConflictRes.IsSuccess && docConflictRes.Message.Contains("Bác sĩ đã có lịch hẹn khác");
            results.Add(new TestStepResult("4.1", "Doctor Overlapping Conflict Guard", "API_INTEGRATION", docConflictBlocked, $"Server rejected: {docConflictRes.Message}"));
            Console.WriteLine($"  -> Result: {(docConflictBlocked ? "PASS" : "FAIL")} ({docConflictRes.Message})");

            // 4.2: Patient Overlapping Conflict Guard
            Console.WriteLine("\n[TEST 4.2] Patient Overlapping Conflict Rejection...");
            await EnsureDoctorShiftAsync(1, 1, todayDate, "08:00:00", "17:00:00");
            var patConflictAppt = await CreatePendingApptAsync(6, 1, todayStart, todayEnd);
            var patConflictRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{patConflictAppt}/confirm", new
            {
                assignedDentistUserId = 1,
                roomId = 1,
                dentalChairId = 1,
                scheduledStart = todayStart,
                scheduledEnd = todayEnd
            }, "manager1@dentalcare.com");
            bool patConflictBlocked = !patConflictRes.IsSuccess && patConflictRes.Message.Contains("Bệnh nhân đã có lịch hẹn khác");
            results.Add(new TestStepResult("4.2", "Patient Overlapping Conflict Guard", "API_INTEGRATION", patConflictBlocked, $"Server rejected: {patConflictRes.Message}"));
            Console.WriteLine($"  -> Result: {(patConflictBlocked ? "PASS" : "FAIL")} ({patConflictRes.Message})");

            // 4.3: Exclusive Room Conflict Guard
            Console.WriteLine("\n[TEST 4.3] Exclusive Room Conflict Rejection...");
            await EnsureDoctorShiftAsync(1, 3, todayDate, "08:00:00", "17:00:00");
            var roomConflictAppt = await CreatePendingApptAsync(52, 3, todayStart, todayEnd);
            var roomConflictRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{roomConflictAppt}/confirm", new
            {
                assignedDentistUserId = 1,
                roomId = 2,
                dentalChairId = 4,
                scheduledStart = todayStart,
                scheduledEnd = todayEnd
            }, "manager@dentalcare.com");
            bool roomConflictBlocked = !roomConflictRes.IsSuccess && roomConflictRes.Message.Contains("Phòng khám đã bị trùng lịch");
            results.Add(new TestStepResult("4.3", "Exclusive Room Conflict Guard", "API_INTEGRATION", roomConflictBlocked, $"Server rejected: {roomConflictRes.Message}"));
            Console.WriteLine($"  -> Result: {(roomConflictBlocked ? "PASS" : "FAIL")} ({roomConflictRes.Message})");

            // 4.4: Chair Conflict Guard
            Console.WriteLine("\n[TEST 4.4] Dental Chair Conflict Rejection...");
            var chairSlotStart = todayDate.AddHours(14);
            var chairSlotEnd = chairSlotStart.AddMinutes(30);
            await EnsureDoctorShiftAsync(1, 1, todayDate, "08:00:00", "17:00:00");
            await EnsureDoctorShiftAsync(4, 1, todayDate, "08:00:00", "17:00:00");
            await EnsureChairOccupiedAsync(1, 1, 1, chairSlotStart, chairSlotEnd);
            var chairConflictAppt = await CreatePendingApptAsync(52, 1, chairSlotStart, chairSlotEnd);
            var chairConflictRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{chairConflictAppt}/confirm", new
            {
                assignedDentistUserId = 4,
                roomId = 1,
                dentalChairId = 1,
                scheduledStart = chairSlotStart,
                scheduledEnd = chairSlotEnd
            }, "manager1@dentalcare.com");
            bool chairConflictBlocked = !chairConflictRes.IsSuccess && chairConflictRes.Message.Contains("Ghế nha khoa đã bị trùng lịch");
            results.Add(new TestStepResult("4.4", "Dental Chair Conflict Guard", "API_INTEGRATION", chairConflictBlocked, $"Server rejected: {chairConflictRes.Message}"));
            Console.WriteLine($"  -> Result: {(chairConflictBlocked ? "PASS" : "FAIL")} ({chairConflictRes.Message})");

            // 4.5: Doctor On Leave Guard
            Console.WriteLine("\n[TEST 4.5] Doctor On Leave / Unavailability Guard...");
            var leaveDate = targetDate.AddDays(5);
            var leaveStart = leaveDate.AddHours(9);
            var leaveEnd = leaveStart.AddMinutes(30);
            await EnsureDoctorShiftAsync(4, 3, leaveDate, "08:00:00", "17:00:00");
            await SetDoctorLeaveAsync(4, leaveStart.AddHours(-1), leaveStart.AddHours(2));

            var leaveAppt = await CreatePendingApptAsync(6, 3, leaveStart, leaveEnd);
            var leaveConfirmRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{leaveAppt}/confirm", new
            {
                assignedDentistUserId = 4,
                roomId = 2,
                dentalChairId = 3,
                scheduledStart = leaveStart,
                scheduledEnd = leaveEnd
            }, "manager@dentalcare.com");
            bool leaveBlocked = !leaveConfirmRes.IsSuccess && leaveConfirmRes.Message.Contains("nghỉ phép");
            results.Add(new TestStepResult("4.5", "Doctor Leave / Unavailability Guard", "API_INTEGRATION", leaveBlocked, $"Server rejected: {leaveConfirmRes.Message}"));
            Console.WriteLine($"  -> Result: {(leaveBlocked ? "PASS" : "FAIL")} ({leaveConfirmRes.Message})");

            // 4.6: Cancelled Doctor Shift Guard
            Console.WriteLine("\n[TEST 4.6] Cancelled Doctor Shift Guard...");
            var cancelShiftDate = targetDate.AddDays(6);
            var cancelShiftStart = cancelShiftDate.AddHours(9);
            var cancelShiftEnd = cancelShiftStart.AddMinutes(30);
            await EnsureDoctorShiftAsync(4, 3, cancelShiftDate, "08:00:00", "17:00:00", status: "Cancelled");

            var cancelShiftAppt = await CreatePendingApptAsync(6, 3, cancelShiftStart, cancelShiftEnd);
            var cancelShiftRes = await PostAsync($"{ApiBaseUrl}/api/department/appointments/{cancelShiftAppt}/confirm", new
            {
                assignedDentistUserId = 4,
                roomId = 2,
                dentalChairId = 3,
                scheduledStart = cancelShiftStart,
                scheduledEnd = cancelShiftEnd
            }, "manager@dentalcare.com");
            bool cancelShiftBlocked = !cancelShiftRes.IsSuccess && cancelShiftRes.Message.Contains("hủy");
            results.Add(new TestStepResult("4.6", "Cancelled Doctor Shift Guard", "API_INTEGRATION", cancelShiftBlocked, $"Server rejected: {cancelShiftRes.Message}"));
            Console.WriteLine($"  -> Result: {(cancelShiftBlocked ? "PASS" : "FAIL")} ({cancelShiftRes.Message})");

            // 4.7: Rapid Double-Click Race Guard
            Console.WriteLine("\n[TEST 4.7] Rapid Double-Click Concurrency Guard...");
            var raceStart = todayDate.AddHours(15);
            var raceEnd = raceStart.AddMinutes(30);
            var raceAppt = await CreatePendingApptAsync(52, 1, raceStart, raceEnd);
            await EnsureDoctorShiftAsync(1, 1, todayDate, "08:00:00", "17:00:00");

            var t1 = Task.Run(() => PostAsync($"{ApiBaseUrl}/api/department/appointments/{raceAppt}/confirm", new
            {
                assignedDentistUserId = 1,
                roomId = 1,
                dentalChairId = 2,
                scheduledStart = raceStart,
                scheduledEnd = raceEnd
            }, "manager1@dentalcare.com"));

            var t2 = Task.Run(() => PostAsync($"{ApiBaseUrl}/api/department/appointments/{raceAppt}/confirm", new
            {
                assignedDentistUserId = 1,
                roomId = 1,
                dentalChairId = 2,
                scheduledStart = raceStart,
                scheduledEnd = raceEnd
            }, "manager1@dentalcare.com"));

            var raceResults = await Task.WhenAll(t1, t2);
            int successCount = 0;
            int failCount = 0;
            foreach (var r in raceResults)
            {
                if (r.IsSuccess) successCount++;
                else failCount++;
            }
            bool doubleClickSafe = (successCount == 1 && failCount == 1);
            results.Add(new TestStepResult("4.7", "Rapid Double-Click Guard", "API_INTEGRATION", doubleClickSafe, $"Successes: {successCount}, Rejected: {failCount}"));
            Console.WriteLine($"  -> Result: {(doubleClickSafe ? "PASS" : "FAIL")} (Successes: {successCount}, Rejections: {failCount})");

            // ========================================================================================
            // SUMMARY
            // ========================================================================================
            Console.WriteLine("\n================================================================================");
            Console.WriteLine("                         TEST EXECUTION SUMMARY                                 ");
            Console.WriteLine("================================================================================");
            bool allPassed = true;
            foreach (var r in results)
            {
                Console.WriteLine($"[{r.Code}] [{r.Type}] {r.Name.PadRight(45)}: {(r.Passed ? "PASS" : "FAIL")} - {r.Details}");
                if (!r.Passed) allPassed = false;
            }

            Console.WriteLine($"\n[VERDICT] Overall Result: {(allPassed ? "QA_PASSED" : "QA_BLOCKED")}");
            return allPassed ? 0 : 1;
        }
        finally
        {
            try
            {
                if (!edgeProcess.HasExited)
                {
                    edgeProcess.Kill(true);
                }
            }
            catch { }
            try { Directory.Delete(tempProfile, true); } catch { }
        }
    }

    private static async Task ClearCookiesAndNavigateLoginAsync(CdpClient cdp)
    {
        await cdp.SendAsync("Network.clearBrowserCookies");
        await cdp.SendAsync("Storage.clearDataForOrigin", new
        {
            origin = BaseUrl,
            storageTypes = "all"
        });
        await cdp.NavigateAsync($"{BaseUrl}/Account/Login");
        await Task.Delay(1500);
    }

    private static async Task SubmitLoginFormAsync(CdpClient cdp, string email, string password)
    {
        await cdp.EvaluateAsync($@"
            var passInput = document.querySelector('#loginPasswordInput') || document.querySelector('input[type=""password""]');
            if (passInput) {{
                var form = passInput.closest('form');
                var emailInput = form.querySelector('input[name=""Email""]') || form.querySelector('#Email') || form.querySelector('input[type=""email""]');
                if (emailInput) {{
                    emailInput.value = '{email}';
                    emailInput.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    emailInput.dispatchEvent(new Event('change', {{ bubbles: true }}));
                }}
                passInput.value = '{password}';
                passInput.dispatchEvent(new Event('input', {{ bubbles: true }}));
                passInput.dispatchEvent(new Event('change', {{ bubbles: true }}));

                var submitBtn = form.querySelector('button.btn-dc-primary') || form.querySelector('button[type=""submit""]');
                if (submitBtn) {{
                    submitBtn.click();
                }} else {{
                    form.submit();
                }}
            }}
        ");
    }

    private static async Task VerifyPageContentAsync(
        CdpClient cdp,
        string expectedPathSnippet,
        string[] expectedKeywords,
        string contextName)
    {
        var currentUrl = await cdp.EvaluateAsync<string>("window.location.href") ?? "";
        var title = await cdp.EvaluateAsync<string>("document.title") ?? "";
        var body = await cdp.EvaluateAsync<string>("document.body ? document.body.innerText : ''") ?? "";

        // 1. Strict 404 Check
        if (cdp.LastDocumentStatus == 404 || body.Contains("404 Not Found") || title.Contains("404") || body.Contains("Trang không tìm thấy"))
        {
            throw new InvalidOperationException($"[PAGE VERIFICATION FAILED] HTTP 404 Not Found encountered at {currentUrl} (Context: {contextName})");
        }

        // 2. Route Check
        if (!currentUrl.Contains(expectedPathSnippet, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"[PAGE VERIFICATION FAILED] URL mismatch. Expected snippet '{expectedPathSnippet}', got '{currentUrl}' (Context: {contextName})");
        }

        // 3. Keyword Content Check
        bool keywordFound = false;
        foreach (var kw in expectedKeywords)
        {
            if (body.Contains(kw, StringComparison.OrdinalIgnoreCase) || title.Contains(kw, StringComparison.OrdinalIgnoreCase))
            {
                keywordFound = true;
                break;
            }
        }

        if (!keywordFound)
        {
            throw new InvalidOperationException($"[PAGE VERIFICATION FAILED] None of the expected keywords [{string.Join(", ", expectedKeywords)}] found on page {currentUrl} (Title: '{title}', Context: {contextName})");
        }

        Console.WriteLine($"      [VERIFIED] {contextName} -> URL: {currentUrl} | Title: '{title}'");
    }

    private static async Task CaptureVerifiedScreenshotAsync(CdpClient cdp, string filename)
    {
        await cdp.CaptureScreenshotAsync(filename);
        var fullPath = Path.Combine(ScreenshotDir, filename);
        if (File.Exists(fullPath))
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(fullPath);
            var hashBytes = await sha.ComputeHashAsync(stream);
            var hashStr = Convert.ToHexString(hashBytes);
            Console.WriteLine($"      [HASH SHA256] {filename} => {hashStr} ({stream.Length:N0} bytes)");
        }
    }

    private static async Task CleanDatabaseBeforeTestAsync()
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        EnsureDatabaseGuard(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM Notifications WHERE AppointmentId IN (
                SELECT AppointmentId FROM Appointments 
                WHERE AppointmentCode LIKE 'APT-E2E%' 
                   OR AppointmentCode LIKE 'APT-CONF%' 
                   OR AppointmentCode LIKE 'APT-OCC%'
            );
            DELETE FROM AppointmentStatusHistories WHERE AppointmentId IN (
                SELECT AppointmentId FROM Appointments 
                WHERE AppointmentCode LIKE 'APT-E2E%' 
                   OR AppointmentCode LIKE 'APT-CONF%' 
                   OR AppointmentCode LIKE 'APT-OCC%'
            );
            DELETE FROM Appointments 
            WHERE AppointmentCode LIKE 'APT-E2E%' 
               OR AppointmentCode LIKE 'APT-CONF%' 
               OR AppointmentCode LIKE 'APT-OCC%';

            DELETE FROM DentistAvailability 
            WHERE DentistUserId IN (1, 4) 
              AND Reason LIKE N'%Nghỉ phép thường niên%';
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> CreatePendingApptAsync(long patientId, long deptId, DateTime start, DateTime end)
    {
        long serviceId = (deptId == 3) ? 7 : 1;
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        EnsureDatabaseGuard(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Appointments (
                AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
                ScheduledStart, ScheduledEnd, Status, BookingSource, ReasonForVisit, CreatedAt, UpdatedAt
            ) VALUES (
                @code, @patientId, @deptId, @serviceId, @start, @end, 'PendingDepartmentReview', 'Receptionist', N'Khám QA Conflict Test', SYSUTCDATETIME(), SYSUTCDATETIME()
            );
            SELECT SCOPE_IDENTITY();
        ";
        cmd.Parameters.AddWithValue("@code", $"APT-CONF-{Guid.NewGuid():N}"[..18]);
        cmd.Parameters.AddWithValue("@patientId", patientId);
        cmd.Parameters.AddWithValue("@deptId", deptId);
        cmd.Parameters.AddWithValue("@serviceId", serviceId);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }

    private static async Task EnsureChairOccupiedAsync(long chairId, long roomId, long doctorId, DateTime start, DateTime end)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        EnsureDatabaseGuard(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT 1 FROM Appointments WHERE DentalChairId = @chairId AND Status = 'Confirmed' AND ScheduledStart < @end AND ScheduledEnd > @start)
            BEGIN
                INSERT INTO Appointments (
                    AppointmentCode, PatientUserId, DepartmentId, RequestedServiceId,
                    AssignedDentistUserId, RoomId, DentalChairId,
                    ScheduledStart, ScheduledEnd, Status, BookingSource, ReasonForVisit, CreatedAt, UpdatedAt
                ) VALUES (
                    @code, 24, 1, 1, @docId, @roomId, @chairId, @start, @end, 'Confirmed', 'Receptionist', N'Occupied Chair QA', SYSUTCDATETIME(), SYSUTCDATETIME()
                );
            END
        ";
        cmd.Parameters.AddWithValue("@chairId", chairId);
        cmd.Parameters.AddWithValue("@roomId", roomId);
        cmd.Parameters.AddWithValue("@docId", doctorId);
        cmd.Parameters.AddWithValue("@code", $"APT-OCC-{Guid.NewGuid():N}"[..18]);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EnsureDoctorShiftAsync(long doctorId, long deptId, DateTime date, string startTime, string endTime, string status = "Scheduled")
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        EnsureDatabaseGuard(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM DentistWorkSchedules WHERE DentistUserId = @doctorId AND WorkDate = @date AND DepartmentId = @deptId;
            INSERT INTO DentistWorkSchedules (DentistUserId, DepartmentId, WorkDate, StartTime, EndTime, Status, Note)
            VALUES (@doctorId, @deptId, @date, @start, @end, @status, N'QA Test Shift');
        ";
        cmd.Parameters.AddWithValue("@doctorId", doctorId);
        cmd.Parameters.AddWithValue("@deptId", deptId);
        cmd.Parameters.AddWithValue("@date", date.Date);
        cmd.Parameters.AddWithValue("@start", TimeSpan.Parse(startTime));
        cmd.Parameters.AddWithValue("@end", TimeSpan.Parse(endTime));
        cmd.Parameters.AddWithValue("@status", status);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SetDoctorLeaveAsync(long doctorId, DateTime start, DateTime end)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        EnsureDatabaseGuard(conn);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO DentistAvailability (DentistUserId, StartDateTime, EndDateTime, AvailabilityStatus, Reason, CreatedAt)
            VALUES (@doctorId, @start, @end, 'OnLeave', N'Nghỉ phép thường niên', SYSUTCDATETIME());
        ";
        cmd.Parameters.AddWithValue("@doctorId", doctorId);
        cmd.Parameters.AddWithValue("@start", start);
        cmd.Parameters.AddWithValue("@end", end);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetTokenAsync(string email)
    {
        if (TokenCache.TryGetValue(email, out var cached))
        {
            return cached;
        }

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        using var client = new HttpClient(handler);
        var loginBody = new { email, password = GetQaPassword() };
        var content = new StringContent(JsonSerializer.Serialize(loginBody), Encoding.UTF8, "application/json");
        var res = await client.PostAsync($"{ApiBaseUrl}/api/auth/login", content);
        var str = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(str);
        var token = doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
        TokenCache[email] = token;
        return token;
    }

    private static async Task<ApiResponseStub> PostAsync(string url, object body, string email)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        using var client = new HttpClient(handler);
        var token = await GetTokenAsync(email);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        try
        {
            var res = await client.PostAsync(url, content);
            var str = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(str);
            bool success = doc.RootElement.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            string msg = doc.RootElement.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "" : "";
            return new ApiResponseStub(success, msg);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"      [HTTP EXCEPTION] {ex.Message}");
            return new ApiResponseStub(false, ex.Message);
        }
    }

    public record TestStepResult(string Code, string Name, string Type, bool Passed, string Details);
    public record ApiResponseStub(bool IsSuccess, string Message);
}

// Minimal CDP WebSocket Client with Document Status Tracking
public class CdpClient : IDisposable
{
    private readonly ClientWebSocket _ws = new();
    private readonly Uri _uri;
    private int _id = 0;

    public int LastDocumentStatus { get; private set; } = 200;
    public string LastDocumentUrl { get; private set; } = "";

    public CdpClient(string wsUrl)
    {
        _uri = new Uri(wsUrl);
    }

    public async Task ConnectAsync()
    {
        await _ws.ConnectAsync(_uri, CancellationToken.None);
    }

    public async Task<JsonElement> SendAsync(string method, object? @params = null)
    {
        int id = Interlocked.Increment(ref _id);
        var req = new
        {
            id,
            method,
            @params
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(req));
        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

        var buffer = new byte[1024 * 1024 * 4];
        while (true)
        {
            var res = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var json = Encoding.UTF8.GetString(buffer, 0, res.Count);
            using var doc = JsonDocument.Parse(json);

            // Track CDP Network responseReceived for Document types
            if (doc.RootElement.TryGetProperty("method", out var methodProp))
            {
                var evMethod = methodProp.GetString();
                if (evMethod == "Network.responseReceived" && doc.RootElement.TryGetProperty("params", out var pProp))
                {
                    if (pProp.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "Document" &&
                        pProp.TryGetProperty("response", out var respProp))
                    {
                        if (respProp.TryGetProperty("status", out var sProp))
                        {
                            LastDocumentStatus = sProp.GetInt32();
                        }
                        if (respProp.TryGetProperty("url", out var uProp))
                        {
                            LastDocumentUrl = uProp.GetString() ?? "";
                        }
                    }
                }
            }

            if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.GetInt32() == id)
            {
                if (doc.RootElement.TryGetProperty("result", out var resultProp))
                {
                    return resultProp.Clone();
                }
                return default;
            }
        }
    }

    public async Task NavigateAsync(string url)
    {
        LastDocumentStatus = 0;
        await SendAsync("Page.navigate", new { url });
    }

    public async Task<T?> EvaluateAsync<T>(string expression)
    {
        var res = await SendAsync("Runtime.evaluate", new
        {
            expression,
            returnByValue = true
        });

        if (res.TryGetProperty("result", out var resultObj) &&
            resultObj.TryGetProperty("value", out var valProp))
        {
            return JsonSerializer.Deserialize<T>(valProp.GetRawText());
        }
        return default;
    }

    public async Task EvaluateAsync(string expression)
    {
        await SendAsync("Runtime.evaluate", new { expression });
    }

    public async Task CaptureScreenshotAsync(string filename)
    {
        var res = await SendAsync("Page.captureScreenshot", new { format = "png" });
        if (res.TryGetProperty("data", out var dataProp))
        {
            var base64 = dataProp.GetString()!;
            var bytes = Convert.FromBase64String(base64);
            var targetPath = Path.Combine(@"D:\SWP\SWP391_Group6\reports\screenshots", filename);
            await File.WriteAllBytesAsync(targetPath, bytes);
            Console.WriteLine($"      [SCREENSHOT] Saved: reports/screenshots/{filename} ({bytes.Length:N0} bytes)");
        }
    }

    public void Dispose()
    {
        _ws.Dispose();
    }
}
