-- ================================================================================================
-- DENTALCARE - MASTER DATABASE VIETNAMESE & UNICODE STANDARDIZATION
-- Applies comprehensive corrections for all mojibake/corrupted characters across all tables.
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> [1/10] Standardizing DepartmentServices...';
    
    -- Correct 'Flour' typo
    UPDATE DepartmentServices
    SET ServiceName = N'Khám nha khoa trẻ em & Bôi Fluor ngừa sâu',
        Description = N'Chăm sóc răng miệng định kỳ và ngừa sâu răng cho trẻ.'
    WHERE DepartmentServiceId = 10;

    UPDATE DepartmentServices
    SET Description = N'Trám xoang sâu răng sữa bằng vật liệu phóng thích Fluor GIC'
    WHERE DepartmentServiceId = 17;

    -- Standardize BookingInstructions
    UPDATE DepartmentServices
    SET BookingInstructions = N'Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.'
    WHERE DepartmentServiceId IN (1, 2, 3, 4, 5, 10, 17, 18, 19, 20);

    UPDATE DepartmentServices
    SET BookingInstructions = N'Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.'
    WHERE DepartmentServiceId IN (6, 7, 8, 9, 11, 12, 13, 21, 22, 23, 24, 25);

    UPDATE DepartmentServices
    SET BookingInstructions = N'Dịch vụ chỉnh nha / thẩm mỹ chuyên sâu. Buổi hẹn đầu sẽ bao gồm quét dấu răng 3D và tư vấn phác đồ.'
    WHERE DepartmentServiceId IN (14, 15, 16);

    UPDATE DepartmentServices
    SET BookingInstructions = N'Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám.'
    WHERE DepartmentServiceId IN (26, 27, 28, 29, 30, 31);


    PRINT '>>> [2/10] Standardizing Medicines...';
    
    -- Unit
    UPDATE Medicines SET Unit = N'Viên' WHERE Unit LIKE N'%Vi%n%' OR Unit LIKE N'%Viê%';
    UPDATE Medicines SET Unit = N'Chai' WHERE Unit LIKE N'%Chai%';
    UPDATE Medicines SET Unit = N'Tuýp' WHERE Unit LIKE N'%Tu%p%';
    UPDATE Medicines SET Unit = N'Gói' WHERE Unit LIKE N'%G%i%';

    -- DosageForm
    UPDATE Medicines SET DosageForm = N'Viên nang' WHERE DosageForm LIKE N'%Vi%nang%';
    UPDATE Medicines SET DosageForm = N'Viên nén' WHERE DosageForm LIKE N'%Vi%n%n%' AND DosageForm NOT LIKE N'%bao phim%';
    UPDATE Medicines SET DosageForm = N'Viên nén bao phim' WHERE DosageForm LIKE N'%bao phim%';
    UPDATE Medicines SET DosageForm = N'Viên sủi' WHERE DosageForm LIKE N'%s%i%';
    UPDATE Medicines SET DosageForm = N'Viên ngậm' WHERE DosageForm LIKE N'%ng%m%';
    UPDATE Medicines SET DosageForm = N'Dung dịch súc miệng' WHERE DosageForm LIKE N'%Dung d%ch%';

    -- Medicine 8 Name
    UPDATE Medicines SET MedicineName = N'Nước súc miệng Chlorhexidine 0.12%' WHERE MedicineId = 8;

    -- UsageInstructions
    UPDATE Medicines SET UsageInstructions = N'Uống sau ăn. Người lớn 1 viên x 3 lần/ngày, cách 8 giờ.' WHERE MedicineId = 1;
    UPDATE Medicines SET UsageInstructions = N'Uống ngay đầu bữa ăn để giảm khó chịu dạ dày. 1 viên x 2 lần/ngày.' WHERE MedicineId = 2;
    UPDATE Medicines SET UsageInstructions = N'Uống khi đau nhức hoặc sốt > 38.5°C. Mỗi lần 1-2 viên, cách nhau tối thiểu 4-6 giờ.' WHERE MedicineId = 3;
    UPDATE Medicines SET UsageInstructions = N'Hòa tan 1 viên vào 200ml nước ấm khi đau dữ dội sau tiểu phẫu. Tối đa 4 viên/ngày.' WHERE MedicineId = 4;
    UPDATE Medicines SET UsageInstructions = N'Kháng viêm giảm đau sau nhổ răng hoặc điều trị tủy. Uống sau ăn no.' WHERE MedicineId = 5;
    UPDATE Medicines SET UsageInstructions = N'Đặc trị nhiễm trùng răng miệng, viêm nha chu, áp xe nướu. Uống trong bữa ăn.' WHERE MedicineId = 6;
    UPDATE Medicines SET UsageInstructions = N'Ngậm dưới lưỡi 2 viên x 2-3 lần/ngày để chống phù nề sau phẫu thuật hàm mặt.' WHERE MedicineId = 7;
    UPDATE Medicines SET UsageInstructions = N'Súc miệng 15ml trong 30 giây sau khi chải răng, 2 lần/ngày. Không được nuốt.' WHERE MedicineId = 8;


    PRINT '>>> [3/10] Standardizing BlogPosts...';
    
    UPDATE BlogPosts
    SET Title = N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa',
        Summary = N'Tủy răng được ví như "trái tim" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm. Cùng tìm hiểu nguyên nhân, dấu hiệu nhận biết và quy trình điều trị viêm tủy răng chuẩn y khoa, an toàn, không đau tại phòng khám.'
    WHERE BlogPostId = 1;

    UPDATE BlogPosts
    SET Title = N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome',
        Summary = N'Răng khôn (răng số 8) mọc lệch, mọc ngầm thường gây sưng đau, sốt, viêm lợi trùm và phá hủy răng số 7 kề bên. Cùng tìm hiểu thời điểm vàng nên nhổ răng khôn và công nghệ nhổ răng siêu âm Piezotome giúp giảm chấn thương, hạn chế chảy máu và nhanh hồi phục.'
    WHERE BlogPostId = 2;

    UPDATE BlogPosts
    SET Title = N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
        Summary = N'Mất răng không chỉ ảnh hưởng đến thẩm mỹ và chức năng ăn nhai mà còn dẫn đến tiêu xương hàm, lão hóa cơ mặt. Cấy ghép Implant là kỹ thuật hiện đại duy nhất phục hồi trọn vẹn cả chân răng và thân răng, mang lại cảm giác ăn nhai vững chắc như răng thật.'
    WHERE BlogPostId = 3;

    UPDATE BlogPosts
    SET Title = N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?',
        Summary = N'Bạn đang phân vân giữa niềng răng mắc cài truyền thống và niềng răng khay trong suốt Invisalign? Bài viết phân tích chi tiết ưu nhược điểm, chi phí và hiệu quả của từng phương pháp giúp bạn dễ dàng đưa ra quyết định phù hợp nhất.'
    WHERE BlogPostId = 4;

    UPDATE BlogPosts
    SET Title = N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp',
        Summary = N'Đánh răng 2 lần mỗi ngày đã thực sự đủ để bảo vệ hàm răng của bạn khỏi sâu răng và viêm nướu? Khám phá 6 thói quen chăm sóc răng miệng chuẩn nha khoa từ việc chải răng đúng kỹ thuật Bass đến sử dụng chỉ nha khoa và máy tăm nước.'
    WHERE BlogPostId = 5;

    UPDATE BlogPosts
    SET Title = N'Nha Khoa DentalCare chính thức khai trương Trung Tâm Chẩn Đoán Hình Ảnh & Laser 3D',
        Summary = N'Hệ thống máy chụp CT ConeBeam 3D thế hệ mới nhất cùng công nghệ Laser hiện đại đã chính thức đi vào hoạt động nhằm nâng cao chất lượng chẩn đoán và điều trị.'
    WHERE BlogPostId = 6;

    UPDATE BlogPosts
    SET Title = N'[Bản Nháp] Cẩm nang chăm sóc răng miệng khoa học cho người niềng răng',
        Summary = N'Tổng hợp các nguyên tắc vệ sinh mắc cài, sử dụng sáp nha khoa và chế độ dinh dưỡng phù hợp cho bệnh nhân chỉnh nha.'
    WHERE BlogPostId = 7;

    UPDATE BlogPosts
    SET Title = N'[Thông Báo] Lịch trực cấp cứu nha khoa và đăng ký khám ưu tiên cuối tuần',
        Summary = N'Thông tin chi tiết về ca trực của các bác sĩ chuyên khoa vào thứ Bảy và Chủ Nhật nhằm hỗ trợ bệnh nhân kịp thời.'
    WHERE BlogPostId = 8;

    UPDATE BlogPosts
    SET Title = N'Cẩm nang chăm sóc răng miệng sau khi nhổ răng khôn',
        Slug = 'cam-nang-cham-soc-rang-mieng-sau-khi-nho-rang-khon',
        Summary = N'Những lưu ý quan trọng cần biết trong 48 giờ đầu sau phẫu thuật nhổ răng khôn.',
        Content = N'<p>Sau khi nhổ răng khôn, bệnh nhân cần cắn chặt bông gòn trong 45 phút đầu và không súc miệng bằng nước muối đậm đặc trong 24 giờ đầu. Vệ sinh nhẹ nhàng bằng bàn chải lông mềm và tuân thủ đơn thuốc của bác sĩ.</p>'
    WHERE BlogPostId = 12;


    PRINT '>>> [4/10] Standardizing StaffProfiles & DentistProfiles...';
    
    UPDATE StaffProfiles SET FullName = N'BS. Vũ Minh Tuấn' WHERE UserId = 47;
    UPDATE StaffProfiles SET FullName = N'Quản Trị Viên Kỹ Thuật (Backup)' WHERE UserId = 43;
    UPDATE StaffProfiles SET FullName = N'Lễ Tân Lê Thu Hương (Thu Ngân)' WHERE UserId = 44;
    UPDATE StaffProfiles SET FullName = N'Lễ Tân Trần Minh Quân (CSKH)' WHERE UserId = 45;
    UPDATE StaffProfiles SET FullName = N'BS. Hoàng Trọng Nghĩa' WHERE UserId = 46;

    UPDATE DentistProfiles
    SET Qualification = N'Tiến sĩ Răng Hàm Mặt, ĐHYD TP.HCM',
        Biography = N'Chuyên gia Cấy ghép Implant và Phẫu thuật hàm mặt kỹ thuật cao với hơn 15 năm kinh nghiệm lâm sàng.'
    WHERE UserId = 4;

    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ CKII Răng Hàm Mặt',
        Biography = N'Chuyên gia Điều trị tủy và Phục hình nha khoa tổng quát chuyên sâu với hơn 18 năm kinh nghiệm.'
    WHERE UserId = 10;

    UPDATE DentistProfiles
    SET Qualification = N'Thạc sĩ Chỉnh nha Thẩm mỹ',
        Biography = N'Chuyên gia Niềng răng khay trong suốt Invisalign và Thẩm mỹ nụ cười toàn diện.'
    WHERE UserId = 11;

    UPDATE DentistProfiles
    SET Qualification = N'Thạc sĩ Nha khoa Trẻ em',
        Biography = N'Bác sĩ chuyên khoa Nha khoa Trẻ em và Tiền chỉnh nha can thiệp sớm cho thanh thiếu niên.'
    WHERE UserId = 12;

    UPDATE DentistProfiles
    SET Qualification = N'Chuyên gia Phục hình Thẩm mỹ DSD',
        Biography = N'Thiết kế nụ cười chuẩn tỷ lệ vàng và dán sứ Veneer thẩm mỹ.'
    WHERE UserId = 46;

    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ Nha khoa Bảo tồn',
        Biography = N'Tận tụy, chuyên sâu lấy cao răng siêu âm và điều trị bảo tồn.'
    WHERE UserId = 47;


    PRINT '>>> [5/10] Standardizing PatientProfiles...';
    
    UPDATE PatientProfiles SET FullName = N'Võ Văn Đông' WHERE UserId = 23 AND FullName = 'DONG VO';
    UPDATE PatientProfiles SET Address = N'45 Hai Bà Trưng, Q.1, TP.HCM' WHERE UserId = 18;
    UPDATE PatientProfiles SET Address = N'120 Nguyễn Thị Minh Khai, Q.3, TP.HCM' WHERE UserId = 19;
    UPDATE PatientProfiles SET Address = N'78 Lê Văn Sỹ, Q.3, TP.HCM' WHERE UserId = 20;
    UPDATE PatientProfiles SET Address = N'230 Phan Xích Long, Phú Nhuận, TP.HCM' WHERE UserId = 21;
    UPDATE PatientProfiles SET Address = N'15 Hoàng Hoa Thám, Bình Thạnh, TP.HCM' WHERE UserId = 22;
    UPDATE PatientProfiles SET Address = N'55 Nguyễn Thị Minh Khai, Quận 1, TP.HCM' WHERE UserId = 48;
    UPDATE PatientProfiles SET Address = N'18 Nguyễn Đình Chiểu, Quận 3, TP.HCM' WHERE UserId = 49;
    UPDATE PatientProfiles SET Address = N'Villa 08 Thảo Điền, TP. Thủ Đức, TP.HCM' WHERE UserId = 50;

    UPDATE PatientProfiles SET EmergencyContact = N'Chồng: 0938999888' WHERE UserId = 18;
    UPDATE PatientProfiles SET EmergencyContact = N'Vợ: 0977111222' WHERE UserId = 19;
    UPDATE PatientProfiles SET EmergencyContact = N'Mẹ: 0986000111' WHERE UserId = 20;
    UPDATE PatientProfiles SET EmergencyContact = N'Bố: 0905445566' WHERE UserId = 21;
    UPDATE PatientProfiles SET EmergencyContact = N'Mẹ: 0918999000' WHERE UserId = 22;
    UPDATE PatientProfiles SET EmergencyContact = N'Vợ: 0988456789' WHERE UserId = 48;
    UPDATE PatientProfiles SET EmergencyContact = N'Chồng: 0988567890' WHERE UserId = 49;
    UPDATE PatientProfiles SET EmergencyContact = N'Thư ký: 0988678901' WHERE UserId = 50;


    PRINT '>>> [6/10] Standardizing DentistWorkSchedules & DepartmentWorkSchedules...';
    
    UPDATE DentistWorkSchedules SET Note = N'Lịch khám chuyên khoa Chỉnh Nha' WHERE DentistUserId = 46 AND (Note LIKE '%Ch%nh Nha%' OR Note LIKE '%L%ch%');
    UPDATE DentistWorkSchedules SET Note = N'Lịch khám chuyên khoa Tổng Quát' WHERE DentistUserId = 47 AND (Note LIKE '%T%ng Qu%t%' OR Note LIKE '%L%ch%');


    PRINT '>>> [7/10] Standardizing Invoices & InvoiceItems...';
    
    UPDATE InvoiceItems SET Description = N'Tiểu phẫu nhổ răng khôn mọc ngầm kẹt xương R38' WHERE InvoiceItemId = 1;
    UPDATE InvoiceItems SET Description = N'Chụp phim X-quang Panorama toàn cảnh KTS' WHERE InvoiceItemId = 2;
    UPDATE InvoiceItems SET Description = N'Đơn thuốc điều trị sau phẫu thuật (Kháng sinh, giảm đau)' WHERE InvoiceItemId = 3;
    UPDATE InvoiceItems SET Description = N'Hàn trám răng thẩm mỹ Composite xoang I (2 răng)' WHERE InvoiceItemId = 4;
    UPDATE InvoiceItems SET Description = N'Lấy cao răng và đánh bóng 2 hàm' WHERE InvoiceItemId = 5;
    UPDATE InvoiceItems SET Description = N'Khám tổng quát & tư vấn chuyên khoa răng miệng' WHERE InvoiceItemId = 7;
    UPDATE InvoiceItems SET Description = N'Lấy cao răng & đánh bóng 2 hàm công nghệ siêu âm Cavitron' WHERE InvoiceItemId = 8;


    PRINT '>>> [8/10] Standardizing Appointments & Histories & Odontogram...';
    
    UPDATE OdontogramEntries SET Condition = N'Răng khôn ngầm', Note = N'Đã tiểu phẫu nhổ bỏ an toàn' WHERE OdontogramEntryId = 22;

    UPDATE AppointmentStatusHistories SET Note = N'Check-in tiếp nhận' WHERE Note LIKE N'%Check-in ti%p%';
    UPDATE AppointmentStatusHistories SET Note = N'Check-in phẫu thuật' WHERE Note LIKE N'%Check-in ph%u%';
    UPDATE AppointmentStatusHistories SET Note = N'Bệnh nhân check-in tại quầy tiếp đón' WHERE HistoryId IN (94, 98, 116) OR Note = 'Check-in';
    UPDATE AppointmentStatusHistories SET Note = N'Lễ tân tiếp nhận bệnh nhân vào khám' WHERE HistoryId = 150;
    UPDATE AppointmentStatusHistories SET Note = N'Bác sĩ xác nhận hoàn tất khám và điều trị' WHERE HistoryId = 152;

    UPDATE Appointments SET ReasonForVisit = N'Đau nhức răng hàm và ê buốt khi ăn nhai' WHERE AppointmentId = 82;
    UPDATE Appointments SET ReasonForVisit = N'Đau răng vùng hàm dưới cần kiểm tra và điều trị' WHERE AppointmentId = 84;
    UPDATE Appointments SET ReasonForVisit = N'Đau răng hàm mặt nhai cần khám tổng quát' WHERE AppointmentId = 85;
    UPDATE Appointments SET ReasonForVisit = N'Đau buốt răng khi uống nước lạnh, cần khám tủy' WHERE AppointmentId = 86;
    UPDATE Appointments SET ReasonForVisit = N'Đau nhức răng hàm và ê buốt khi ăn uống' WHERE AppointmentId = 102;


    PRINT '>>> [9/10] Standardizing ClinicalEntries & Referrals & Prescriptions...';
    
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

    UPDATE ImagingRecords 
    SET ResultSummary = N'Phim Panorama cho thấy răng 38 mọc lệch ngang 90 độ, chân răng gần sát ống thần kinh răng dưới.' 
    WHERE ImagingRecordId = 2;

    UPDATE DepartmentReferrals 
    SET Reason = N'Bệnh nhân có nhu cầu chỉnh nha niềng răng trong suốt Invisalign' 
    WHERE ReferralId = 1;

    UPDATE DepartmentReferrals 
    SET Reason = N'Bệnh nhân có răng số 46 vỡ lớn sát cuống không thể bảo tồn tủy. Đề nghị chuyển Khoa Phẫu Thuật để nhổ răng và cấy ghép Implant tức thì.' 
    WHERE ReferralId = 2;

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


    PRINT '>>> [10/10] Standardizing FollowUpSchedules & VisitFeedback...';
    
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
    PRINT '================================================================================================';
    PRINT 'ALL DATABASE MOJIBAKE AND FONT ENCODING ERRORS CORRECTED SUCCESSFULLY!';
    PRINT '================================================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR OCCURRED: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
