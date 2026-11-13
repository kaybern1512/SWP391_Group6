-- ============================================================================
-- SCRIPT: Fix_All_Database_Vietnamese_Complete.sql
-- PURPOSE: Sửa triệt để TOÀN BỘ lỗi font, lỗi Mojikake và chuẩn hóa dữ liệu tiếng Việt
--          trong toàn bộ cơ sở dữ liệu DentalClinicManagementDB.
-- Target: DentalClinicManagementDB
-- ============================================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

PRINT N'>>> 1. CHUẨN HÓA THÔNG TIN NHÂN SỰ & HỒ SƠ BÁC SĨ (StaffProfiles, DentistProfiles)...';
UPDATE StaffProfiles SET FullName = N'BS. Vũ Minh Tuấn' WHERE UserId = 47 OR FullName COLLATE Latin1_General_BIN LIKE N'%VÅ© Minh Tuáº¥n%';
UPDATE StaffProfiles SET FullName = N'Quản Trị Viên Kỹ Thuật (Backup)' WHERE UserId = 43;
UPDATE StaffProfiles SET FullName = N'Lễ Tân Lê Thu Hương (Thu Ngân)' WHERE UserId = 44;
UPDATE StaffProfiles SET FullName = N'Lễ Tân Trần Minh Quân (CSKH)' WHERE UserId = 45;
UPDATE StaffProfiles SET FullName = N'BS. Hoàng Trọng Nghĩa' WHERE UserId = 46;

UPDATE DentistProfiles SET Qualification = N'Chuyên gia Phục hình Thẩm mỹ DSD', Biography = N'Thiết kế nụ cười chuẩn tỷ lệ vàng và dán sứ Veneer thẩm mỹ.' WHERE UserId = 46;
UPDATE DentistProfiles SET Qualification = N'Bác sĩ Nha khoa Bảo tồn', Biography = N'Tận tụy, chuyên sâu lấy cao răng siêu âm và điều trị bảo tồn.' WHERE UserId = 47;

PRINT N'>>> 2. CHUẨN HÓA HỒ SƠ BỆNH NHÂN (PatientProfiles)...';
UPDATE PatientProfiles SET FullName = N'Võ Văn Đông' WHERE UserId = 23 AND FullName = 'DONG VO';

UPDATE PatientProfiles SET Address = N'45 Hai Bà Trưng, Q.1, TP.HCM' WHERE PatientCode = 'PAT-202610-0007' OR UserId = 18;
UPDATE PatientProfiles SET Address = N'120 Nguyễn Thị Minh Khai, Q.3, TP.HCM' WHERE PatientCode = 'PAT-202610-0008' OR UserId = 19;
UPDATE PatientProfiles SET Address = N'78 Lê Văn Sỹ, Q.3, TP.HCM' WHERE PatientCode = 'PAT-202610-0009' OR UserId = 20;
UPDATE PatientProfiles SET Address = N'230 Phan Xích Long, Phú Nhuận, TP.HCM' WHERE PatientCode = 'PAT-202610-0010' OR UserId = 21;
UPDATE PatientProfiles SET Address = N'15 Hoàng Hoa Thám, Bình Thạnh, TP.HCM' WHERE PatientCode = 'PAT-202610-0011' OR UserId = 22;
UPDATE PatientProfiles SET Address = N'55 Nguyễn Thị Minh Khai, Quận 1, TP.HCM' WHERE PatientCode = 'PAT-202610-0015' OR UserId = 48;
UPDATE PatientProfiles SET Address = N'18 Nguyễn Đình Chiểu, Quận 3, TP.HCM' WHERE PatientCode = 'PAT-202610-0016' OR UserId = 49;
UPDATE PatientProfiles SET Address = N'Villa 08 Thảo Điền, TP. Thủ Đức, TP.HCM' WHERE PatientCode = 'PAT-202610-0017' OR UserId = 50;

UPDATE PatientProfiles SET EmergencyContact = N'Chồng: 0938999888' WHERE UserId = 18;
UPDATE PatientProfiles SET EmergencyContact = N'Vợ: 0977111222' WHERE UserId = 19;
UPDATE PatientProfiles SET EmergencyContact = N'Mẹ: 0986000111' WHERE UserId = 20;
UPDATE PatientProfiles SET EmergencyContact = N'Bố: 0905445566' WHERE UserId = 21;
UPDATE PatientProfiles SET EmergencyContact = N'Mẹ: 0918999000' WHERE UserId = 22;
UPDATE PatientProfiles SET EmergencyContact = N'Vợ - 0988456789' WHERE UserId = 48;
UPDATE PatientProfiles SET EmergencyContact = N'Chồng - 0988567890' WHERE UserId = 49;
UPDATE PatientProfiles SET EmergencyContact = N'Thư ký - 0988678901' WHERE UserId = 50;

PRINT N'>>> 3. CHUẨN HÓA LỊCH LÀM VIỆC BÁC SĨ (DentistWorkSchedules)...';
UPDATE DentistWorkSchedules SET Note = N'Lịch khám chuyên khoa Chỉnh Nha' WHERE DentistUserId = 46 AND (Note COLLATE Latin1_General_BIN LIKE N'%Lá»‹ch khÃ¡m chuyÃªn khoa Chá»‰nh Nha%' OR Note LIKE '%Chỉnh Nha%');
UPDATE DentistWorkSchedules SET Note = N'Lịch khám chuyên khoa Tổng Quát' WHERE DentistUserId = 47 AND (Note COLLATE Latin1_General_BIN LIKE N'%Lá»‹ch khÃ¡m chuyÃªn khoa Tá»•ng QuÃ¡t%' OR Note LIKE '%Tổng Quát%');

PRINT N'>>> 4. CHUẨN HÓA HÓA ĐƠN VÀ CHI TIẾT DỊCH VỤ (InvoiceItems)...';
UPDATE InvoiceItems SET Description = N'Tiểu phẫu nhổ răng khôn mọc ngầm kẹt xương R38' WHERE InvoiceItemId = 1;
UPDATE InvoiceItems SET Description = N'Chụp phim X-quang Panorama toàn cảnh KTS' WHERE InvoiceItemId = 2;
UPDATE InvoiceItems SET Description = N'Đơn thuốc điều trị sau phẫu thuật (Kháng sinh, giảm đau)' WHERE InvoiceItemId = 3;
UPDATE InvoiceItems SET Description = N'Hàn trám răng thẩm mỹ Composite xoang I (2 răng)' WHERE InvoiceItemId = 4;
UPDATE InvoiceItems SET Description = N'Lấy cao răng và đánh bóng 2 hàm' WHERE InvoiceItemId = 5;

PRINT N'>>> 5. CHUẨN HÓA SƠ ĐỒ RĂNG (OdontogramEntries)...';
UPDATE OdontogramEntries SET Condition = N'Răng khôn ngầm', Note = N'Đã tiểu phẫu nhổ bỏ an toàn' WHERE OdontogramEntryId = 22;

PRINT N'>>> 6. CHUẨN HÓA TRẠNG THÁI LỊCH HẸN (AppointmentStatusHistories)...';
UPDATE AppointmentStatusHistories SET Note = N'Check-in tiếp nhận' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Check-in tiáº¿p nháº­n%';
UPDATE AppointmentStatusHistories SET Note = N'Check-in phẫu thuật' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Check-in pháº«u thuáº­t%';
UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân check-in tại quầy tiếp đón' WHERE HistoryId IN (94, 98, 116) OR Note = 'Check-in';
UPDATE AppointmentStatusHistories SET Note = N'Lễ tân tiếp nhận bệnh nhân vào khám' WHERE HistoryId = 150;
UPDATE AppointmentStatusHistories SET Note = N'Bác sĩ xác nhận hoàn tất khám và điều trị' WHERE HistoryId = 152;

PRINT N'>>> 7. CHUẨN HÓA LÝ DO KHÁM BỆNH (Appointments)...';
UPDATE Appointments SET ReasonForVisit = N'Đau nhức răng hàm và ê buốt khi ăn nhai' WHERE AppointmentId = 82 OR AppointmentCode = 'APP-20261014-347E09';
UPDATE Appointments SET ReasonForVisit = N'Đau răng vùng hàm dưới cần kiểm tra và điều trị' WHERE AppointmentId = 84 OR AppointmentCode = 'APP-20261014-A5CFB5';
UPDATE Appointments SET ReasonForVisit = N'Đau răng hàm mặt nhai cần khám tổng quát' WHERE AppointmentId = 85 OR AppointmentCode = 'APP-20261014-BF4BF5';
UPDATE Appointments SET ReasonForVisit = N'Đau buốt răng khi uống nước lạnh, cần khám tủy' WHERE AppointmentId = 86 OR AppointmentCode = 'APP-20261014-B679A4';
UPDATE Appointments SET ReasonForVisit = N'Đau nhức răng hàm và ê buốt khi ăn uống' WHERE AppointmentId = 102 OR AppointmentCode = 'APP-20261021-9CDF46';

PRINT N'>>> 8. CHUẨN HÓA HỒ SƠ LÂM SÀNG VÀ CHẨN ĐOÁN (ClinicalEntries)...';
UPDATE ClinicalEntries 
SET Symptoms = N'Răng ê buốt nhẹ khi uống nước lạnh', 
    ClinicalFindings = N'Rãnh sâu ngà nông mặt nhai R35', 
    Diagnosis = N'Sâu ngà nông răng 35', 
    ClinicalNotes = N'Chỉ định trám thẩm mỹ Composite'
WHERE ClinicalEntryId = 5;

UPDATE ClinicalEntries 
SET Symptoms = N'Đau nhức răng hàm khi nhai đồ cứng', 
    ClinicalFindings = N'Vỡ mẻ múi ngoài răng 46', 
    Diagnosis = N'Nứt vỡ thân răng 46', 
    ClinicalNotes = N'Chỉ định điều trị tủy và bọc sứ bảo tồn'
WHERE ClinicalEntryId = 6;

UPDATE ClinicalEntries 
SET Symptoms = N'Sâu răng hàm gây dắt thức ăn', 
    ClinicalFindings = N'Lỗ sâu kẽ R16-R17 sâu ngà trung bình', 
    Diagnosis = N'Sâu kẽ răng 16, 17', 
    ClinicalNotes = N'Nạo sạch ngà mủn, hàn trám xoang II Composite'
WHERE ClinicalEntryId = 7;

UPDATE ClinicalEntries 
SET Symptoms = N'Ê buốt răng khi ăn đồ nóng lạnh', 
    ClinicalFindings = N'Mòn cổ răng mặt ngoài R24, R25', 
    Diagnosis = N'Mòn ngót cổ răng (K03.1)', 
    ClinicalNotes = N'Hàn trám bít cổ răng men ngà bằng GIC Fuji IX'
WHERE ClinicalEntryId = 10;

PRINT N'>>> 9. CHUẨN HÓA HÌNH ẢNH X-QUANG & PHIẾU CHUYỂN KHOA...';
UPDATE ImagingRecords 
SET ResultSummary = N'Phim Panorama cho thấy răng 38 mọc lệch ngang 90 độ, chân răng gần sát ống thần kinh răng dưới.' 
WHERE ImagingRecordId = 2;

UPDATE DepartmentReferrals 
SET Reason = N'Bệnh nhân sau khi nhổ răng khôn có nhu cầu niềng răng do răng chen chúc hàm dưới. Đã chuyển hồ sơ sang Khoa Chỉnh Nha.' 
WHERE ReferralId = 1;

UPDATE DepartmentReferrals 
SET Reason = N'Bệnh nhân có răng số 46 vỡ lớn sát cuống không thể bảo tồn tủy. Đề nghị chuyển Khoa Phẫu Thuật để nhổ răng và cấy ghép Implant tức thì.' 
WHERE ReferralId = 2;

PRINT N'>>> 10. CHUẨN HÓA ĐƠN THUỐC (Prescriptions & PrescriptionItems)...';
UPDATE Prescriptions 
SET GeneralInstructions = N'Uống thuốc sau bữa ăn no 15-30 phút. Uống nhiều nước, kiêng rượu bia và thuốc lá trong thời gian dùng thuốc.' 
WHERE PrescriptionId = 3 AND (GeneralInstructions IS NULL OR GeneralInstructions = '');

UPDATE Prescriptions 
SET GeneralInstructions = N'Uống thuốc đúng giờ sau ăn no. Cắn gạc chặt trong 60 phút. Không súc miệng nước muối mạnh hay khạc nhổ trong 24h đầu. Chườm lạnh má ngày đầu, chườm ấm ngày thứ 2.' 
WHERE PrescriptionId = 4;

UPDATE PrescriptionItems 
SET MedicationName = N'Augmentin 1g (Amoxicillin/Clavulanate)',
    Dosage = N'1 viên',
    Frequency = N'2 lần/ngày',
    Duration = N'5 ngày',
    Instructions = N'Uống sáng 1v, tối 1v sau khi ăn no'
WHERE PrescriptionItemId = 8;

UPDATE PrescriptionItems 
SET MedicationName = N'Ibuprofen 400mg',
    Dosage = N'1 viên',
    Frequency = N'3 lần/ngày',
    Duration = N'3 ngày',
    Instructions = N'Uống khi đau nhiều, cách nhau ít nhất 6 tiếng'
WHERE PrescriptionItemId = 9;

UPDATE PrescriptionItems 
SET MedicationName = N'Alphachymotrypsin 4.2mg',
    Dosage = N'2 viên',
    Frequency = N'3 lần/ngày',
    Duration = N'5 ngày',
    Instructions = N'Ngậm dưới lưỡi giảm phù nề, sưng má'
WHERE PrescriptionItemId = 10;

UPDATE PrescriptionItems 
SET MedicationName = N'Nước súc miệng Kin Gingival 0.12%',
    Dosage = N'15ml',
    Frequency = N'2 lần/ngày',
    Duration = N'7 ngày',
    Instructions = N'Bắt đầu súc nhẹ từ ngày thứ 2 trở đi sau ăn'
WHERE PrescriptionItemId = 11;

PRINT N'>>> 11. CHUẨN HÓA LỊCH HẸN TÁI KHÁM & ĐÁNH GIÁ (FollowUps, VisitFeedback)...';
UPDATE FollowUpSchedules 
SET Reason = N'Tái khám kiểm tra vết thương và cắt chỉ vicryl vùng nhổ răng 38' 
WHERE FollowUpId = 1;

UPDATE FollowUpSchedules 
SET Reason = N'Tái khám kiểm tra tình trạng sâu răng và phục hồi mô răng' 
WHERE FollowUpId = 2;

UPDATE VisitFeedback 
SET Comment = N'Dịch vụ khám chu đáo, phòng khám sạch sẽ và bác sĩ tư vấn rất nhiệt tình.' 
WHERE FeedbackId = 38;

UPDATE VisitFeedback 
SET Comment = N'Răng sứ đẹp xuất sắc, cười tự tin hơn hẳn.' 
WHERE FeedbackId = 28;

COMMIT TRANSACTION;
PRINT N'>>> ĐÃ HOÀN TẤT CHUẨN HÓA TIẾNG VIỆT TOÀN BỘ DATABASE THÀNH CÔNG!';
GO
