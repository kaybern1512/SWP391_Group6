-- ============================================================================
-- SCRIPT: Fix_Font_Mojikake_Clinical_Data.sql
-- PURPOSE: Sửa triệt để tất cả các lỗi font chữ / lỗi mã hóa Mojikake (UTF-8 -> CP1252)
--          trong toàn bộ cơ sở dữ liệu DentalClinicManagementDB.
-- ============================================================================

USE DentalClinicManagementDB;
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG APPOINTMENTS...';

UPDATE Appointments SET ReasonForVisit = N'Răng hàm R16 ê buốt khi ăn đồ ngọt, có lỗ sẫm màu' WHERE AppointmentCode = 'APP-20261012-010';
UPDATE Appointments SET ReasonForVisit = N'Chảy máu chân răng khi đánh răng buổi sáng, có mảng bám vôi răng' WHERE AppointmentCode = 'APP-20261012-011';
UPDATE Appointments SET ReasonForVisit = N'Đau nhức dữ dội răng hàm dưới R46, đau giật lên tai ban đêm' WHERE AppointmentCode = 'APP-20261012-012';
UPDATE Appointments SET ReasonForVisit = N'Khám định kỳ 6 tháng kiểm tra tổng quát toàn bộ hàm răng' WHERE AppointmentCode = 'APP-20261012-013';
UPDATE Appointments SET ReasonForVisit = N'Sứt mẻ góc răng cửa do cắn vật cứng, cần hàn phục hình thẩm mỹ' WHERE AppointmentCode = 'APP-20261012-014';
UPDATE Appointments SET ReasonForVisit = N'Răng xỉn màu do uống trà cà phê thường xuyên, muốn tẩy trắng cấp tốc' WHERE AppointmentCode = 'APP-20261012-020';
UPDATE Appointments SET ReasonForVisit = N'Tái khám định kỳ siết dây cung và thay thun chỉnh nha tháng thứ 3' WHERE AppointmentCode = 'APP-20261012-021';
UPDATE Appointments SET ReasonForVisit = N'Răng khôn hàm dưới R48 mọc lệch nghiêng 90 độ đâm vào răng số 7' WHERE AppointmentCode = 'APP-20261012-030';
UPDATE Appointments SET ReasonForVisit = N'Bé mới mọc răng số 6 vĩnh viễn, muốn trám bít hố rãnh phòng ngừa sâu răng' WHERE AppointmentCode = 'APP-20261012-040';
UPDATE Appointments SET ReasonForVisit = N'Bôi vecni Flour phòng chống sâu men răng sữa toàn hàm' WHERE AppointmentCode = 'APP-20261012-041';

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG APPOINTMENT STATUS HISTORIES...';

UPDATE AppointmentStatusHistories
SET Note = N'Hệ thống tự động đồng bộ lịch hẹn được phê duyệt.'
WHERE NewStatus = 'Confirmed'
  AND (Note COLLATE Latin1_General_BIN LIKE N'%Ã%' OR Note COLLATE Latin1_General_BIN LIKE N'%Ä%' OR Note COLLATE Latin1_General_BIN LIKE N'%á»%');

UPDATE AppointmentStatusHistories
SET Note = N'Lễ tân tiếp đón và cấp số thứ tự vào phòng khám.'
WHERE NewStatus = 'CheckedIn'
  AND (Note COLLATE Latin1_General_BIN LIKE N'%Ã%' OR Note COLLATE Latin1_General_BIN LIKE N'%Ä%' OR Note COLLATE Latin1_General_BIN LIKE N'%á»%');

UPDATE AppointmentStatusHistories
SET Note = N'Bác sĩ hoàn tất khám và lưu chẩn đoán bệnh án.'
WHERE NewStatus = 'Completed'
  AND (Note COLLATE Latin1_General_BIN LIKE N'%Ã%' OR Note COLLATE Latin1_General_BIN LIKE N'%Ä%' OR Note COLLATE Latin1_General_BIN LIKE N'%á»%');

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG CLINICAL ENTRIES...';

-- Ca 1 (APP-20261012-010, Võ Văn Đông)
UPDATE ce
SET ce.Symptoms = N'Ê buốt nhẹ mặt nhai R16 khi uống nước lạnh hoặc nhai thức ăn chua.',
    ce.ClinicalFindings = N'Xoang sâu ngà nông sẫm màu mặt nhai R16 và R26; mảng bám vôi răng độ 1 quanh cổ răng R31-R41.',
    ce.Diagnosis = N'Sâu ngà nông K02.1 mặt nhai R16, R26; Viêm nướu viền nhẹ do mảng bám.',
    ce.DentalConditions = N'Mô mềm hồng hào, khớp cắn Angle hạng I.',
    ce.ClinicalNotes = N'Làm sạch xoang sâu, xoi mòn acid 37%, quét keo dán Single Bond Universal, trám composite thẩm mỹ 3M Z250 quang trùng hợp. Đánh bóng hoàn thiện.'
FROM ClinicalEntries ce
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010';

-- Ca 2 (APP-20261012-011, Lê Hoàng Minh)
UPDATE ce
SET ce.Symptoms = N'Chảy máu nướu răng tự phát khi chải răng, có mùi hôi miệng.',
    ce.ClinicalFindings = N'Vôi răng dưới nướu nhiều toàn hàm, tụt nướu nhẹ 1-2mm vùng răng cửa hàm dưới, túi nha chu nông 3mm.',
    ce.Diagnosis = N'Viêm nha chu mạn tính độ 2 (K05.3), cao răng dưới nướu độ 3.',
    ce.DentalConditions = N'Nướu phù nề sưng đỏ viền quanh các kẽ răng.',
    ce.ClinicalNotes = N'Lấy cao răng toàn phần bằng máy siêu âm Woodpecker, đánh bóng toàn hàm bằng bột Proxyt, bơm rửa túi nha chu bằng Povidine 10%.'
FROM ClinicalEntries ce
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-011';

-- Ca 3 (APP-20261012-020, Đặng Quốc Tuấn)
UPDATE ce
SET ce.Symptoms = N'Răng ố vàng xỉn màu do uống trà, cà phê nhiều năm, mong muốn răng trắng sáng tự nhiên.',
    ce.ClinicalFindings = N'Màu răng ban đầu A3.5 (bảng so màu Vita), men răng chắc khỏe, không có lỗ sâu hoạt động.',
    ce.Diagnosis = N'Nhiễm màu ngoại sinh men răng do sắc tố thực phẩm độ 3.',
    ce.DentalConditions = N'Mô mềm săn chắc, đường cười cân đối.',
    ce.ClinicalNotes = N'Bảo vệ viền nướu bằng composite lỏng Gingival Barrier, quét thuốc tẩy trắng Opalescence Boost 40%, chiếu đèn Laser Beyond Whitening 3 chu kỳ x 15 phút. Nâng tông lên màu A1.'
FROM ClinicalEntries ce
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-020';

-- Ca 4 (APP-20261012-040, Bé Vũ Hải Đăng)
UPDATE ce
SET ce.Symptoms = N'Mẹ đưa bé đi khám phòng ngừa sâu răng khi mới mọc răng hàm vĩnh viễn đầu tiên.',
    ce.ClinicalFindings = N'Răng số 6 (16, 26, 36, 46) đã nhú hoàn toàn khỏi nướu, rãnh giải phẫu mặt nhai rất sâu và hẹp, đọng mảng bám.',
    ce.Diagnosis = N'Hố rãnh sâu nguy cơ cao sâu răng vĩnh viễn sớm.',
    ce.DentalConditions = N'Răng sữa phát triển tốt, khớp cắn sữa ổn định.',
    ce.ClinicalNotes = N'Làm sạch rãnh nhai bằng chổi cước và bột đánh bóng không flour, xoi mòn men răng, bôi keo trám bít hố rãnh Sealant Clinpro 3M, chiếu đèn quang trùng hợp 20 giây.'
FROM ClinicalEntries ce
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-040';

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG ODONTOGRAM ENTRIES...';

-- Odontogram Ca 1 (APP-20261012-010)
UPDATE oe SET oe.Note = N'Trám composite quang trùng hợp 3M Z250' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010' AND oe.ToothNumber = '16';

UPDATE oe SET oe.Note = N'Trám composite dự phòng sâu tái phát' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010' AND oe.ToothNumber = '26';

UPDATE oe SET oe.Note = N'Viêm nướu viền, cạo vôi siêu âm' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010' AND oe.ToothNumber IN ('31', '41');

UPDATE oe SET oe.Note = N'Răng khôn mọc nghiêng gần, theo dõi nhổ' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010' AND oe.ToothNumber = '18';

UPDATE oe SET oe.Note = N'Răng khôn mọc kẹt trong xương hàm dưới' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010' AND oe.ToothNumber = '48';

-- Odontogram Ca 2 (APP-20261012-011)
UPDATE oe SET oe.Note = N'Mão răng sứ kim loại cũ, khít sát tốt' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-011' AND oe.ToothNumber = '36';

UPDATE oe SET oe.Note = N'Viêm tủy mạn tính cần điều trị nội nha đợt sau' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-011' AND oe.ToothNumber = '46';

UPDATE oe SET oe.Note = N'Răng khỏe mạnh bình thường' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-011' AND oe.ToothNumber IN ('11', '21');

-- Odontogram Ca 3 (APP-20261012-020)
UPDATE oe SET oe.Note = N'Tẩy trắng thành công lên tone A1' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-020' AND oe.ToothNumber IN ('11', '12', '21', '22');

-- Odontogram Ca 4 (APP-20261012-040)
UPDATE oe SET oe.Note = N'Trám bít hố rãnh ngừa sâu răng Sealant Clinpro 3M' FROM OdontogramEntries oe
INNER JOIN ClinicalEntries ce ON oe.ClinicalEntryId = ce.ClinicalEntryId
INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-040' AND oe.ToothNumber IN ('16', '26', '36', '46');

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG TREATMENT PLAN ITEMS...';

-- Kế hoạch điều trị bệnh nhân 7
UPDATE tpi SET tpi.Note = N'Cạo vôi răng siêu âm và đánh bóng toàn hàm' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 7 AND tpi.SequenceNo = 1;

UPDATE tpi SET tpi.Note = N'Trám composite xoang sâu ngà mặt nhai R16' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 7 AND tpi.SequenceNo = 2;

UPDATE tpi SET tpi.Note = N'Nội nha chữa tủy răng cối lớn 3 ống tủy' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 7 AND tpi.SequenceNo = 3;

UPDATE tpi SET tpi.Note = N'Tái khám kiểm tra sau điều trị tủy' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 7 AND tpi.SequenceNo = 4;

-- Kế hoạch điều trị bệnh nhân 15
UPDATE tpi SET tpi.Note = N'Khám tư vấn và chụp phim Cephalo phân tích nụ cười' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 15 AND tpi.SequenceNo = 1;

UPDATE tpi SET tpi.Note = N'Gắn mắc cài kim loại tự buộc 2 hàm Damon Q' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 15 AND tpi.SequenceNo = 2;

UPDATE tpi SET tpi.Note = N'Theo dõi kéo răng và chỉnh trục khớp cắn' FROM TreatmentPlanItems tpi
INNER JOIN TreatmentPlans tp ON tpi.TreatmentPlanId = tp.TreatmentPlanId
WHERE tp.PatientUserId = 15 AND tpi.SequenceNo = 3;

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG TREATMENT SESSIONS...';

UPDATE TreatmentSessions
SET TreatmentResult = N'Đã trám hoàn tất R16 bằng composite 3M Z250, khớp cắn chuẩn không cộm vướng.',
    ClinicalNote = N'Bệnh nhân ăn nhai tốt, dặn dò không nhai đồ quá cứng trong 24 giờ đầu.'
WHERE TreatmentResult COLLATE Latin1_General_BIN LIKE N'%Ã%' OR ClinicalNote COLLATE Latin1_General_BIN LIKE N'%Ã%';

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG PRESCRIPTIONS & PRESCRIPTION ITEMS...';

UPDATE Prescriptions
SET GeneralInstructions = N'Uống thuốc sau bữa ăn no 15-30 phút. Uống nhiều nước, kiêng rượu bia và thuốc lá trong thời gian dùng thuốc.'
WHERE GeneralInstructions COLLATE Latin1_General_BIN LIKE N'%Ã%';

UPDATE PrescriptionItems
SET MedicationName = N'Amoxicillin 500mg',
    Dosage = N'1 viên',
    Frequency = N'2 lần/ngày (Sáng - Tối)',
    Duration = N'5 ngày',
    Instructions = N'Kháng sinh chống nhiễm khuẩn sau can thiệp'
WHERE MedicationName LIKE '%Amoxicillin%';

UPDATE PrescriptionItems
SET MedicationName = N'Paracetamol 500mg',
    Dosage = N'1 viên',
    Frequency = N'Khi đau (cách nhau tối thiểu 4-6h)',
    Duration = N'3 ngày',
    Instructions = N'Giảm đau, hạ sốt khi có triệu chứng'
WHERE MedicationName LIKE '%Paracetamol%';

UPDATE PrescriptionItems
SET MedicationName = N'Alpha Chymotrypsin',
    Dosage = N'2 viên',
    Frequency = N'3 lần/ngày',
    Duration = N'5 ngày',
    Instructions = N'Kháng viêm, chống phù nề mô quanh răng'
WHERE MedicationName LIKE '%Alpha Chymotrypsin%';

UPDATE PrescriptionItems
SET MedicationName = N'Nước súc miệng Chlorhexidine 0.12%',
    Dosage = N'15ml',
    Frequency = N'2 lần/ngày sau khi chải răng',
    Duration = N'7 ngày',
    Instructions = N'Kháng khuẩn khoang miệng và nướu răng'
WHERE MedicationName LIKE '%Chlorhexidine%';

PRINT N'>>> ĐANG CẬP NHẬT LẠI TIẾNG VIỆT CHUẨN CHO BẢNG VISIT FEEDBACK...';

UPDATE vf
SET vf.Comment = N'Bác sĩ Hùng tay nghề rất cao và nhẹ nhàng, trám răng hoàn toàn không đau buốt! Phòng khám sạch đẹp và trang thiết bị rất hiện đại.'
FROM VisitFeedback vf
INNER JOIN Appointments a ON vf.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-010';

UPDATE vf
SET vf.Comment = N'Cạo vôi răng bằng sóng siêu âm rất êm ái, sạch bóng mảng bám. Nhân viên lễ tân tiếp đón niềm nở và hướng dẫn tận tình.'
FROM VisitFeedback vf
INNER JOIN Appointments a ON vf.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-011';

UPDATE vf
SET vf.Comment = N'Tẩy trắng răng xong lên tone trắng sáng rõ rệt, nụ cười tự tin hơn hẳn. Bác sĩ Thảo dặn dò chế độ kiêng cữ rất chi tiết.'
FROM VisitFeedback vf
INNER JOIN Appointments a ON vf.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-020';

UPDATE vf
SET vf.Comment = N'Bác sĩ Mai Lan dỗ bé rất khéo, phòng khám nhi có tranh vẽ và đồ chơi vui nhộn nên bé không hề sợ hãi hay quấy khóc!'
FROM VisitFeedback vf
INNER JOIN Appointments a ON vf.AppointmentId = a.AppointmentId
WHERE a.AppointmentCode = 'APP-20261012-040';

PRINT N'>>> HOÀN TẤT SỬA LỖI FONT CHỮ TOÀN BỘ CƠ SỞ DỮ LIỆU THÀNH CÔNG!';
