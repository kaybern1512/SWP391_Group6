-- ============================================================================
-- SCRIPT: Fix_Font_Mojikake_Clinical_Data.sql
-- PURPOSE: Sửa triệt để tất cả các lỗi font chữ / lỗi mã hóa Mojikake (UTF-8 -> CP1252)
--          trong toàn bộ cơ sở dữ liệu DentalClinicManagementDB.
-- Target: DentalClinicManagementDB
-- ============================================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO STAFF & DENTIST PROFILES...';

-- 1. StaffProfiles
UPDATE StaffProfiles SET FullName = N'Quản Trị Viên Kỹ Thuật (Backup)' WHERE UserId = 43 OR EmployeeCode = 'EMP-ADM-002';
UPDATE StaffProfiles SET FullName = N'Lễ Tân Lê Thu Hương (Thu Ngân)' WHERE UserId = 44 OR EmployeeCode = 'EMP-REC-002';
UPDATE StaffProfiles SET FullName = N'Lễ Tân Trần Minh Quân (CSKH)' WHERE UserId = 45 OR EmployeeCode = 'EMP-REC-003';
UPDATE StaffProfiles SET FullName = N'BS. Hoàng Trọng Nghĩa' WHERE UserId = 46 OR EmployeeCode = 'EMP-DEN-005';

-- 2. DentistProfiles
UPDATE DentistProfiles SET Qualification = N'Chuyên gia Phục hình Thẩm mỹ DSD', Biography = N'Thiết kế nụ cười chuẩn tỷ lệ vàng và dán sứ Veneer thẩm mỹ.' WHERE UserId = 46;
UPDATE DentistProfiles SET Qualification = N'Bác sĩ Nha khoa Bảo tồn', Biography = N'Tận tụy, chuyên sâu lấy cao răng siêu âm và điều trị bảo tồn.' WHERE UserId = 47;

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO PATIENT PROFILES...';

-- 3. PatientProfiles
UPDATE PatientProfiles SET Address = N'45 Hai Bà Trưng, Q.1, TP.HCM' WHERE PatientCode = 'PAT-202610-0007' OR UserId = 18;
UPDATE PatientProfiles SET Address = N'78 Lê Văn Sỹ, Q.3, TP.HCM' WHERE PatientCode = 'PAT-202610-0009' OR UserId = 20;
UPDATE PatientProfiles SET Address = N'230 Phan Xích Long, Phú Nhuận, TP.HCM' WHERE PatientCode = 'PAT-202610-0010' OR UserId = 21;
UPDATE PatientProfiles SET Address = N'15 Hoàng Hoa Thám, Bình Thạnh, TP.HCM' WHERE PatientCode = 'PAT-202610-0011' OR UserId = 22;
UPDATE PatientProfiles SET Address = N'18 Nguyễn Đình Chiểu, Quận 3, TP.HCM' WHERE PatientCode = 'PAT-202610-0016' OR UserId = 49;
UPDATE PatientProfiles SET Address = N'120 Nguyễn Thị Minh Khai, Q.3, TP.HCM' WHERE PatientCode = 'PAT-202610-0008';
UPDATE PatientProfiles SET Address = N'55 Nguyễn Thị Minh Khai, Quận 1, TP.HCM' WHERE PatientCode = 'PAT-202610-0015';
UPDATE PatientProfiles SET Address = N'Villa 08 Thảo Điền, TP. Thủ Đức, TP.HCM' WHERE PatientCode = 'PAT-202610-0017';

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO CLINICAL & REFERRALS & FOLLOW-UPS...';

-- 4. DepartmentReferrals
UPDATE DepartmentReferrals SET Reason = N'Bệnh nhân có nhu cầu chỉnh nha niềng răng trong suốt Invisalign' WHERE ReferralId = 1;
UPDATE DepartmentReferrals SET Reason = N'Bệnh nhân có răng số 46 vỡ lớn sát cuống không thể bảo tồn tủy. Đề nghị chuyển Khoa Phẫu Thuật để nhổ răng và cấy ghép Implant tức thì.' WHERE ReferralId = 2;

-- 5. FollowUpSchedules
UPDATE FollowUpSchedules SET Reason = N'Tái khám kiểm tra vết thương và cắt chỉ vicryl vùng nhổ răng 38' WHERE FollowUpId = 1;

-- 6. TreatmentSessions
UPDATE TreatmentSessions SET 
    ClinicalNote = N'Đã nhổ trọn vẹn răng 38, bệnh nhân tỉnh táo, ổn định.',
    TreatmentResult = N'Nhổ răng thành công không gãy chân răng, cầm máu tốt.'
WHERE TreatmentSessionId = 4;

-- 7. TreatmentPlanItems
UPDATE TreatmentPlanItems SET Note = N'Tiểu phẫu nhổ răng khôn lệch' WHERE TreatmentPlanItemId = 10;
UPDATE TreatmentPlanItems SET Note = N'Cắt chỉ và kiểm tra liền thương' WHERE TreatmentPlanItemId = 11;

-- 8. ClinicalEntries
UPDATE ClinicalEntries SET 
    Symptoms = N'Sưng đau vùng góc hàm trái, há miệng hạn chế, ăn nhai cấn má.',
    ClinicalFindings = N'Răng 38 mọc lệch 90 độ, lợi trùm viêm đỏ có mủ nhẹ, dắt thức ăn kẽ R37-R38.',
    Diagnosis = N'Răng khôn 38 mọc lệch ngầm kẹt xương (K01.1), viêm lợi trùm cấp.',
    DentalConditions = N'Vệ sinh răng miệng trung bình.',
    ClinicalNotes = N'Gây tê tại chỗ Lidocain 2%, rạch vạt niêm mạc màng xương, chia cắt thân và chân răng bằng tay khoan siêu âm, nhổ răng trọn vẹn, khâu đóng 2 mũi Vicryl 4.0.'
WHERE ClinicalEntryId = 9;

-- 8.1 ImagingRecords
UPDATE ImagingRecords SET ResultSummary = N'Phim Panorama cho thấy răng 38 mọc lệch ngang 90 độ, chân răng gần sát ống thần kinh răng dưới.' WHERE ImagingRecordId = 2;

-- 9. Prescriptions & PrescriptionItems
UPDATE Prescriptions SET GeneralInstructions = N'Uống thuốc sau bữa ăn no 15-30 phút. Uống nhiều nước, kiêng rượu bia và thuốc lá trong thời gian dùng thuốc.' WHERE PrescriptionId = 3 AND (GeneralInstructions IS NULL OR GeneralInstructions = '');
UPDATE Prescriptions SET GeneralInstructions = N'Uống thuốc đúng giờ sau ăn no. Cắn gạc chặt trong 60 phút. Không súc miệng nước muối mạnh hay khạc nhổ trong 24h đầu. Chườm lạnh má ngày đầu, chườm ấm ngày thứ 2.' WHERE PrescriptionId = 4;

UPDATE PrescriptionItems SET Dosage = N'1 viên', Frequency = N'2 lần/ngày', Duration = N'5 ngày', Instructions = N'Uống sáng 1v, tối 1v sau khi ăn no' WHERE PrescriptionItemId = 8;
UPDATE PrescriptionItems SET Dosage = N'1 viên', Frequency = N'3 lần/ngày', Duration = N'3 ngày', Instructions = N'Uống khi đau nhiều, cách nhau ít nhất 6 tiếng' WHERE PrescriptionItemId = 9;
UPDATE PrescriptionItems SET Dosage = N'2 viên', Frequency = N'3 lần/ngày', Duration = N'5 ngày', Instructions = N'Ngậm dưới lưỡi giảm phù nề, sưng má' WHERE PrescriptionItemId = 10;
UPDATE PrescriptionItems SET MedicationName = N'Nước súc miệng Kin Gingival 0.12%', Dosage = N'15ml', Frequency = N'2 lần/ngày', Duration = N'7 ngày', Instructions = N'Bắt đầu súc nhẹ từ ngày thứ 2 trở đi sau ăn' WHERE PrescriptionItemId = 11;

-- 10. Notifications
UPDATE Notifications SET Subject = N'Nhắc lịch hẹn tái khám cắt chỉ', Content = N'Bạn có lịch hẹn tái khám kiểm tra vết mổ và cắt chỉ vào ngày 22/10/2026 tại Khoa Phẫu Thuật Miệng.' WHERE NotificationId IN (15, 17);
UPDATE Notifications SET Subject = N'Ca chuyển khoa mới cần duyệt', Content = N'Bác sĩ Khoa Răng Tổng Quát vừa chuyển 1 ca bệnh nhân sang chuyên khoa của bạn để cấy ghép Implant.' WHERE NotificationId IN (16, 18);

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO APPOINTMENTS...';

-- 11. Appointments
UPDATE Appointments SET ReasonForVisit = N'Bọc răng sứ thẩm mỹ Cercon HT cho 4 răng cửa hàm trên' WHERE AppointmentCode = 'APP-20261015-WAIT';
UPDATE Appointments SET ReasonForVisit = N'Khám và điều trị tủy răng cối lớn hàm dưới R36' WHERE AppointmentCode = 'APP-20261015-CHAIR';
UPDATE Appointments SET ReasonForVisit = N'Khám tổng quát, nhổ răng khôn mọc lệch và vệ sinh cạo vôi răng' WHERE AppointmentCode = 'APP-20261015-DONE';
UPDATE Appointments SET ReasonForVisit = N'Khám răng định kỳ và hàn răng sâu ngà mặt nhai R26' WHERE AppointmentCode = 'APP-20261015-CHECKIN';
UPDATE Appointments SET ReasonForVisit = N'Nhổ răng khôn mọc lệch R38 đâm ngang răng số 7' WHERE AppointmentCode = 'APP-20261015-PREV3';
UPDATE Appointments SET ReasonForVisit = N'Răng hàm R46 sâu vỡ lớn đau buốt về đêm, cần chữa tủy khẩn cấp' WHERE AppointmentCode = 'APP-20261015-PREC1';
UPDATE Appointments SET ReasonForVisit = N'Khám răng tổng quát và hàn răng thẩm mỹ Composite răng cửa' WHERE AppointmentCode = 'APP-20261015-PK1';
UPDATE Appointments SET ReasonForVisit = N'Khám răng sữa cho bé, bôi vecni Flour chống sâu răng toàn hàm' WHERE AppointmentCode = 'APP-20261015-PK4';
UPDATE Appointments SET ReasonForVisit = N'Hàn trám răng thẩm mỹ 2 răng sâu mặt nhai R15, R16' WHERE AppointmentCode = 'APP-20261015-UNPAID';

UPDATE Appointments SET ReasonForVisit = N'Lấy cao răng siêu âm định kỳ' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Láº¥y cao rÄƒng%';
UPDATE Appointments SET ReasonForVisit = N'Tẩy trắng răng Laser Whitening' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Táº©y tráº¯ng rÄƒng%';
UPDATE Appointments SET ReasonForVisit = N'Khám nha khoa trẻ em & Bôi Flour ngừa sâu' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%KhÃ¡m nha khoa tráº» em%';
UPDATE Appointments SET ReasonForVisit = N'Trám răng thẩm mỹ Composite' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%TrÃ¡m rÄƒng%';
UPDATE Appointments SET ReasonForVisit = N'Tiểu phẫu nhổ răng khôn' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Tiá»ƒu pháº«u%';
UPDATE Appointments SET ReasonForVisit = N'Chữa tủy răng công nghệ cao' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Chá»¯a tá»§y%';
UPDATE Appointments SET ReasonForVisit = N'Bọc răng sứ thẩm mỹ Cercon HT' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Bá» c rÄƒng sá»©%';
UPDATE Appointments SET ReasonForVisit = N'Khám và tư vấn niềng răng' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Niá» ng rÄƒng%';
UPDATE Appointments SET ReasonForVisit = N'Cấy ghép Implant Straumann' WHERE ReasonForVisit COLLATE Latin1_General_BIN LIKE N'%Cáº¥y ghÃ©p Implant%';

UPDATE Appointments SET ReasonForVisit = N'Khám & Tư vấn Chỉnh nha' WHERE AppointmentCode = 'APP-20261010-002';
UPDATE Appointments SET ReasonForVisit = N'Nhổ răng khôn ngầm mọc ngang sóng Piezotome' WHERE AppointmentCode = 'APP-20261010-003';
UPDATE Appointments SET ReasonForVisit = N'Điều trị viêm nha chu & Nạo túi lợi' WHERE AppointmentCode = 'APP-20261010-005';
UPDATE Appointments SET ReasonForVisit = N'Dán sứ Veneer Emax cao cấp' WHERE AppointmentCode = 'APP-20261010-006';
UPDATE Appointments SET ReasonForVisit = N'Điều trị tủy răng công nghệ cao' WHERE AppointmentCode = 'APP-20261009-001';
UPDATE Appointments SET ReasonForVisit = N'Niềng răng mắc cài kim loại chuẩn' WHERE AppointmentCode = 'APP-20261009-002';
UPDATE Appointments SET ReasonForVisit = N'Khám chuyên sâu & Tư vấn cấy ghép Implant' WHERE AppointmentCode = 'APP-20261009-004';
UPDATE Appointments SET ReasonForVisit = N'Nhổ răng sữa lung lay vô cảm nhẹ nhàng' WHERE AppointmentCode = 'APP-20261009-005';
UPDATE Appointments SET ReasonForVisit = N'Khám tổng quát định kỳ' WHERE AppointmentCode = 'APP-20261008-001';
UPDATE Appointments SET ReasonForVisit = N'Bọc răng toàn sứ Cercon HT chính hãng' WHERE AppointmentCode = 'APP-20261008-002';
UPDATE Appointments SET ReasonForVisit = N'Cấy ghép trụ Implant Dentium Hàn Quốc' WHERE AppointmentCode = 'APP-20261008-003';
UPDATE Appointments SET ReasonForVisit = N'Trám bít hố rãnh ngừa sâu răng vĩnh viễn' WHERE AppointmentCode = 'APP-20261007-001';
UPDATE Appointments SET ReasonForVisit = N'Hàn răng thẩm mỹ Inlay / Onlay Porcelain' WHERE AppointmentCode = 'APP-20261007-002';
UPDATE Appointments SET ReasonForVisit = N'Đính đá thẩm mỹ lên răng' WHERE AppointmentCode = 'APP-20261007-003';
UPDATE Appointments SET ReasonForVisit = N'Phẫu thuật cắt chóp & Nạo nang nha chu' WHERE AppointmentCode = 'APP-20261007-004';
UPDATE Appointments SET ReasonForVisit = N'Niềng răng mắc cài sứ thẩm mỹ' WHERE AppointmentCode = 'APP-20261006-002';
UPDATE Appointments SET ReasonForVisit = N'Bệnh nhân đau ê buốt răng số 16 khi ăn nhai, nướu sưng nhẹ' WHERE AppointmentCode = 'APP-20261015-Q001';
UPDATE Appointments SET ReasonForVisit = N'Khám và điều trị tủy răng số 26, răng có lỗ sâu to' WHERE AppointmentCode = 'APP-20261015-INS01';
UPDATE Appointments SET ReasonForVisit = N'Khám răng định kỳ 6 tháng và cạo vôi răng siêu âm' WHERE AppointmentCode = 'APP-20261015-CONF1';
UPDATE Appointments SET ReasonForVisit = N'Tư vấn niềng răng mắc cài kim loại tự buộc cho răng thưa và khấp khểnh' WHERE AppointmentCode = 'APP-20261015-PEND1';

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO VISIT FEEDBACK...';

-- 12. VisitFeedback
UPDATE VisitFeedback SET Comment = N'Bác sĩ Hùng làm rất êm, không hề buốt răng, phòng khám sạch đẹp.' WHERE FeedbackId IN (5, 21);
UPDATE VisitFeedback SET Comment = N'Hiệu quả tẩy trắng rất rõ, tư vấn tận tình chu đáo!' WHERE FeedbackId IN (6, 22);
UPDATE VisitFeedback SET Comment = N'Bác sĩ Lan dỗ trẻ con rất khéo, bé nhà mình không hề sợ hãi.' WHERE FeedbackId IN (7, 23);
UPDATE VisitFeedback SET Comment = N'Nhổ răng khôn nhanh chóng, thuốc tê tốt nên không đau chút nào.' WHERE FeedbackId IN (8, 24);
UPDATE VisitFeedback SET Comment = N'Bác sĩ Thảo thao tác nhẹ nhàng, miếng trám màu sắc tự nhiên.' WHERE FeedbackId = 25;
UPDATE VisitFeedback SET Comment = N'Dịch vụ rất chuyên nghiệp, tiếp đón chu đáo.' WHERE FeedbackId = 26;
UPDATE VisitFeedback SET Comment = N'Máy Piezotome chạy êm ru, vết mổ mau lành.' WHERE FeedbackId = 27;
UPDATE VisitFeedback SET Comment = N'Răng sứ đẹp xuất sắc, cười tự tin hơn hẳn.' WHERE FeedbackId = 28;
UPDATE VisitFeedback SET Comment = N'Tái khám niềng răng nhanh gọn, bác sĩ dặn dò kỹ lưỡng.' WHERE FeedbackId = 29;
UPDATE VisitFeedback SET Comment = N'Tư vấn Implant rất chi tiết, có phim 3D giải thích rõ ràng.' WHERE FeedbackId = 30;
UPDATE VisitFeedback SET Comment = N'Răng sứ khít sát, ăn nhai hoàn toàn bình thường.' WHERE FeedbackId = 31;
UPDATE VisitFeedback SET Comment = N'Ca cấy ghép rất êm ái, trang thiết bị phòng mổ hiện đại chuẩn vô trùng.' WHERE FeedbackId = 32;
UPDATE VisitFeedback SET Comment = N'Miếng Onlay khít khao ăn nhai chắc chắn.' WHERE FeedbackId = 33;
UPDATE VisitFeedback SET Comment = N'Mắc cài sứ rất trong và thẩm mỹ, bác sĩ làm cẩn thận.' WHERE FeedbackId = 34;
UPDATE VisitFeedback SET Comment = N'Bác sĩ Hùng tay nghề rất cao, vết nhổ lành nhanh chóng.' WHERE FeedbackId = 35;
UPDATE VisitFeedback SET Comment = N'Phòng khám đẹp và sạch sẽ, các bạn lễ tân tiếp đón nhiệt tình.' WHERE FeedbackId = 36;
UPDATE VisitFeedback SET Comment = N'Bác sĩ Hùng nhổ răng khôn cực kỳ êm ái, chỉ mất 15 phút là xong. Phòng khám sạch sẽ, hiện đại, lễ tân nhiệt tình!' WHERE FeedbackId = 37;

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO APPOINTMENT STATUS HISTORIES...';

-- 13. AppointmentStatusHistories
UPDATE AppointmentStatusHistories SET Note = N'Đính đá răng nanh thẩm mỹ' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Ä Ã­nh Ä‘Ã¡ rÄƒng%';
UPDATE AppointmentStatusHistories SET Note = N'Đặt trụ Implant Dentium vào vị trí răng 36 thành công' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Ä áº·t trá»¥ Implant Dentium%';
UPDATE AppointmentStatusHistories SET Note = N'Bác sĩ hoàn thành cạo vôi răng và đánh bóng' WHERE Note COLLATE Latin1_General_BIN LIKE N'%BÃ¡c sÄ© hoÃ n thÃ nh cáº¡o vÃ´i%';
UPDATE AppointmentStatusHistories SET Note = N'Bé hợp tác tốt, đã bôi Flour bảo vệ men răng' WHERE Note COLLATE Latin1_General_BIN LIKE N'%BÃ© há»£p tÃ¡c tá»‘t%';
UPDATE AppointmentStatusHistories SET Note = N'Bít hố rãnh 4 răng hàm số 6' WHERE Note COLLATE Latin1_General_BIN LIKE N'%BÃ­t há»‘ rÃ£nh 4 rÄƒng%';
UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân đặt lịch trực tuyến lúc 09:30' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Bá»‡nh nhÃ¢n Ä‘áº·t lá»‹ch trá»±c tuyáº¿n%';
UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân đến quầy làm thủ tục' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Bá»‡nh nhÃ¢n Ä‘áº¿n quáº§y lÃ m thá»§ tá»¥c%';
UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân đến quầy tiếp đón làm thủ tục' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Bá»‡nh nhÃ¢n Ä‘áº¿n quáº§y tiáº¿p Ä‘Ã³n%';
UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân check-in' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Bá»‡nh nhÃ¢n check-in%';
UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân hoàn tất check-in tại quầy tiếp đón' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Bá»‡nh nhÃ¢n hoÃ n táº¥t check-in%';
UPDATE AppointmentStatusHistories SET Note = N'Cắt chóp răng viêm và hàn ngược MTA' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Cáº¯t chÃ³p rÄƒng viÃªm%';
UPDATE AppointmentStatusHistories SET Note = N'Chụp CT ConeBeam và tư vấn phác đồ cấy ghép' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Chá»¥p CT ConeBeam%';
UPDATE AppointmentStatusHistories SET Note = N'Check-in bé' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Check-in bÃ©%';
UPDATE AppointmentStatusHistories SET Note = N'Check-in thủ tục tiếp đón' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Check-in thá»§ tá»¥c tiáº¿p Ä‘Ã³n%';
UPDATE AppointmentStatusHistories SET Note = N'Nạo túi lợi và bơm rửa kháng viêm' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Náº¡o tÃºi lá»£i%';
UPDATE AppointmentStatusHistories SET Note = N'Nhổ 1 răng sữa lung lay nhẹ nhàng' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Nhá»• 1 rÄƒng sá»¯a lung lay%';
UPDATE AppointmentStatusHistories SET Note = N'Nhổ răng số 8 hàm dưới an toàn, kê đơn thuốc' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Nhá»• rÄƒng sá»‘ 8 hÃ m dÆ°á»›i%';
UPDATE AppointmentStatusHistories SET Note = N'Nhổ răng số 8 trên bên phải an toàn' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Nhá»• rÄƒng sá»‘ 8 trÃªn%';
UPDATE AppointmentStatusHistories SET Note = N'Phụ huynh đưa bé đến khám đúng giờ' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Phá»¥ huynh Ä‘Æ°a bÃ©%';
UPDATE AppointmentStatusHistories SET Note = N'Siết cung và thay thun niềng định kỳ' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Siáº¿t cung vÃ  thay thun%';
UPDATE AppointmentStatusHistories SET Note = N'Tẩy trắng hoàn tất' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Táº©y tráº¯ng hoÃ n táº¥t%';
UPDATE AppointmentStatusHistories SET Note = N'Tẩy trắng thành công, răng sáng lên 3 tông màu' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Táº©y tráº¯ng thÃ nh cÃ´ng%';
UPDATE AppointmentStatusHistories SET Note = N'Thử sườn và gắn mão sứ Cercon HT' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Thá»­ sÆ°á» n vÃ  gáº¯n mÃ£o sá»©%';
UPDATE AppointmentStatusHistories SET Note = N'Tiếp tân kiểm tra huyết áp và hồ sơ bệnh nhân' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Tiáº¿p tÃ¢n kiá»ƒm tra huyáº¿t Ã¡p%';
UPDATE AppointmentStatusHistories SET Note = N'Trám 2 răng sữa sâu hoàn tất' WHERE Note COLLATE Latin1_General_BIN LIKE N'%TrÃ¡m 2 rÄƒng sá»¯a sÃ¢u%';
UPDATE AppointmentStatusHistories SET Note = N'Trám đắp composite thẩm mỹ răng 11' WHERE Note COLLATE Latin1_General_BIN LIKE N'%TrÃ¡m Ä‘áº¯p composite%';
UPDATE AppointmentStatusHistories SET Note = N'Trám răng hoàn tất' WHERE Note COLLATE Latin1_General_BIN LIKE N'%TrÃ¡m rÄƒng hoÃ n táº¥t%';
UPDATE AppointmentStatusHistories SET Note = N'Trám xoang sâu loại 1 thành công' WHERE Note COLLATE Latin1_General_BIN LIKE N'%TrÃ¡m xoang sÃ¢u loáº¡i 1%';
UPDATE AppointmentStatusHistories SET Note = N'Trưởng khoa xác nhận lịch hẹn. Số thứ tự khám: Q-015.' WHERE Note COLLATE Latin1_General_BIN LIKE N'%TrÆ°á»Ÿng khoa xÃ¡c nháº­n lá»‹ch háº¹n%';
UPDATE AppointmentStatusHistories SET Note = N'Lễ tân kiểm tra thông tin và chuyển tiếp chuyên khoa' WHERE Note COLLATE Latin1_General_BIN LIKE N'%Lá»… tÃ¢n kiá»ƒm tra thÃ´ng tin%';

UPDATE AppointmentStatusHistories SET Note = N'Khám hoàn tất' WHERE HistoryId = 85;
UPDATE AppointmentStatusHistories SET Note = N'Lấy dấu răng và lập phác đồ chỉnh nha' WHERE HistoryId = 87;
UPDATE AppointmentStatusHistories SET Note = N'Hoàn thành tiểu phẫu Piezotome' WHERE HistoryId = 89;
UPDATE AppointmentStatusHistories SET Note = N'Check-in tiếp nhận bé' WHERE HistoryId IN (90, 114, 126);
UPDATE AppointmentStatusHistories SET Note = N'Gắn veneer 2 răng cửa chuẩn xác' WHERE HistoryId = 95;
UPDATE AppointmentStatusHistories SET Note = N'Làm sạch ống tủy lần 1' WHERE HistoryId = 97;
UPDATE AppointmentStatusHistories SET Note = N'Lấy cao răng hoàn tất' WHERE HistoryId = 101;
UPDATE AppointmentStatusHistories SET Note = N'Khám tổng quát hoàn tất' WHERE HistoryId = 107;
UPDATE AppointmentStatusHistories SET Note = N'Thử sườn và gắn mão sứ Cercon HT' WHERE HistoryId = 109;
UPDATE AppointmentStatusHistories SET Note = N'Đặt trụ Implant Dentium vào vị trí răng 36 thành công' WHERE HistoryId = 111;
UPDATE AppointmentStatusHistories SET Note = N'Gắn Onlay sứ răng 26' WHERE HistoryId = 117;
UPDATE AppointmentStatusHistories SET Note = N'Đính đá răng nanh thẩm mỹ' WHERE HistoryId = 119;
UPDATE AppointmentStatusHistories SET Note = N'Lấy cao răng sạch sẽ' WHERE HistoryId = 123;
UPDATE AppointmentStatusHistories SET Note = N'Gắn mắc cài sứ hàm trên' WHERE HistoryId = 125;
UPDATE AppointmentStatusHistories SET Note = N'Khám và bôi Flour ngừa sâu' WHERE HistoryId = 127;

COMMIT TRANSACTION;
PRINT N'>>> HOÀN TẤT SỬA LỖI FONT CHỮ TOÀN BỘ CƠ SỞ DỮ LIỆU THÀNH CÔNG!';
GO