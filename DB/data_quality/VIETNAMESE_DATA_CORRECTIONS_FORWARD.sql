-- ================================================================================================
-- DENTALCARE - VIETNAMESE DATA QUALITY CORRECTIONS (FORWARD SCRIPT)
-- Database Target: DentalCare_Integration_Dev_QA_Audit (or DentalCare_Integration_Dev after approval)
-- Description: Applies audited, safe Vietnamese corrections with strict conditional checks on old values.
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @TargetDB NVARCHAR(128) = DB_NAME();
PRINT '================================================================================================';
PRINT 'STARTING VIETNAMESE DATA QUALITY CORRECTIONS ON DATABASE: ' + @TargetDB;
PRINT 'TIME: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '================================================================================================';

BEGIN TRANSACTION;

BEGIN TRY
    -- --------------------------------------------------------------------------------------------
    -- 1. DEPARTMENT SERVICES
    -- --------------------------------------------------------------------------------------------
    PRINT '>>> 1. Correcting DepartmentServices...';

    -- 1.1 Correct "Flour" typo in Service 10
    UPDATE DepartmentServices
    SET ServiceName = N'Khám nha khoa trẻ em & Bôi Fluor ngừa sâu',
        Description = N'Chăm sóc răng miệng định kỳ và ngừa sâu răng cho trẻ.'
    WHERE DepartmentServiceId = 10 AND ServiceName LIKE N'%Flour%';
    PRINT '    [DepartmentServices] Service 10 updated: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- 1.2 Restore BookingInstructions for General & Consultation Services (10 min early arrival)
    UPDATE DepartmentServices
    SET BookingInstructions = N'Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.'
    WHERE DepartmentServiceId IN (1, 5, 10) 
      AND (BookingInstructions LIKE '%[%' OR BookingInstructions LIKE '%Vui l%');
    PRINT '    [DepartmentServices] BookingInstructions (Consultation) updated: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- 1.3 Restore BookingInstructions for Surgery / Implant Services (Light meal, no coffee)
    UPDATE DepartmentServices
    SET BookingInstructions = N'Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.'
    WHERE DepartmentServiceId IN (8, 9, 21, 22, 23, 24, 25)
      AND (BookingInstructions LIKE '%[%' OR BookingInstructions LIKE '%chuy%');
    PRINT '    [DepartmentServices] BookingInstructions (Surgery/Implant) updated: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- 1.4 Restore BookingInstructions for Pediatric Services (Gentle encouragement)
    UPDATE DepartmentServices
    SET BookingInstructions = N'Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám.'
    WHERE DepartmentServiceId IN (26, 27, 28, 29, 30, 31)
      AND (BookingInstructions LIKE '%[%' OR BookingInstructions LIKE '%nha khoa tr%');
    PRINT '    [DepartmentServices] BookingInstructions (Pediatric) updated: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- --------------------------------------------------------------------------------------------
    -- 2. MEDICINES
    -- --------------------------------------------------------------------------------------------
    PRINT '>>> 2. Correcting Medicines...';

    -- 2.1 Standardize Unit 'ViÃªn' to 'Viên'
    UPDATE Medicines
    SET Unit = N'Viên'
    WHERE Unit = 'ViÃªn';
    PRINT '    [Medicines] Unit "Viên" standardized: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- 2.2 Restore DosageForm
    UPDATE Medicines SET DosageForm = N'Viên nang' WHERE DosageForm = 'ViÃªn nang';
    UPDATE Medicines SET DosageForm = N'Viên nén' WHERE DosageForm = 'ViÃªn nÃ©n';
    UPDATE Medicines SET DosageForm = N'Viên nén bao phim' WHERE DosageForm = 'ViÃªn nÃ©n bao phim';
    UPDATE Medicines SET DosageForm = N'Viên sủi' WHERE DosageForm = 'ViÃªn sá»§i';
    UPDATE Medicines SET DosageForm = N'Viên ngậm' WHERE DosageForm LIKE 'ViÃªn ngáº%';
    UPDATE Medicines SET DosageForm = N'Dung dịch súc miệng' WHERE DosageForm LIKE 'Dung dá»‹ch%';
    PRINT '    [Medicines] DosageForms restored: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- 2.3 Restore Medicine 8 Name
    UPDATE Medicines
    SET MedicineName = N'Nước súc miệng Chlorhexidine 0.12%'
    WHERE MedicineId = 8 AND MedicineName LIKE N'%NÆ°á»›c%';
    PRINT '    [Medicines] Med 8 Name restored: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- 2.4 Restore UsageInstructions for primary clinical medications
    UPDATE Medicines SET UsageInstructions = N'Uống sau ăn. Người lớn 1 viên x 3 lần/ngày, cách 8 giờ.' WHERE MedicineId = 1 AND UsageInstructions LIKE '%Uá»‘ng%';
    UPDATE Medicines SET UsageInstructions = N'Uống ngay đầu bữa ăn để giảm khó chịu dạ dày. 1 viên x 2 lần/ngày.' WHERE MedicineId = 2 AND UsageInstructions LIKE '%Uá»‘ng%';
    UPDATE Medicines SET UsageInstructions = N'Uống khi đau nhức hoặc sốt > 38.5°C. Mỗi lần 1-2 viên, cách nhau tối thiểu 4-6 giờ.' WHERE MedicineId = 3 AND UsageInstructions LIKE '%Uá»‘ng%';
    UPDATE Medicines SET UsageInstructions = N'Hòa tan 1 viên vào 200ml nước ấm khi đau dữ dội sau tiểu phẫu. Tối đa 4 viên/ngày.' WHERE MedicineId = 4 AND UsageInstructions LIKE '%HÃ²a%';
    UPDATE Medicines SET UsageInstructions = N'Kháng viêm giảm đau sau nhổ răng hoặc điều trị tủy. Uống sau ăn no.' WHERE MedicineId = 5 AND UsageInstructions LIKE '%KhÃ¡ng%';
    UPDATE Medicines SET UsageInstructions = N'Đặc trị nhiễm trùng răng miệng, viêm nha chu, áp xe nướu. Uống trong bữa ăn.' WHERE MedicineId = 6 AND UsageInstructions LIKE '%áº·c trá»‹%';
    UPDATE Medicines SET UsageInstructions = N'Ngậm dưới lưỡi 2 viên x 2-3 lần/ngày để chống phù nề sau phẫu thuật hàm mặt.' WHERE MedicineId = 7 AND UsageInstructions LIKE '%Ngáº%';
    UPDATE Medicines SET UsageInstructions = N'Súc miệng 15ml trong 30 giây sau khi chải răng, 2 lần/ngày. Không được nuốt.' WHERE MedicineId = 8 AND UsageInstructions LIKE '%SÃºc%';
    PRINT '    [Medicines] Clinical UsageInstructions restored: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- --------------------------------------------------------------------------------------------
    -- 3. BLOG POSTS
    -- --------------------------------------------------------------------------------------------
    PRINT '>>> 3. Correcting BlogPosts...';

    UPDATE BlogPosts
    SET Title = N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa',
        Summary = N'Tủy răng được ví như "trái tim" nuôi dưỡng sự sống của chiếc răng. Khi bị viêm tủy, người bệnh thường chịu đựng những cơn đau nhức dữ dội từng cơn, đặc biệt về ban đêm. Cùng tìm hiểu nguyên nhân, dấu hiệu nhận biết và quy trình điều trị viêm tủy răng chuẩn y khoa, an toàn, không đau tại phòng khám.'
    WHERE BlogPostId = 1 AND (Title LIKE '%Ä iá»%' OR Title LIKE '%viÃªm%');

    UPDATE BlogPosts
    SET Title = N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome',
        Summary = N'Răng khôn (răng số 8) mọc lệch, mọc ngầm thường gây sưng đau, sốt, viêm lợi trùm và phá hủy răng số 7 kề bên. Cùng tìm hiểu thời điểm vàng nên nhổ răng khôn và công nghệ nhổ răng siêu âm Piezotome giúp giảm chấn thương, hạn chế chảy máu và nhanh hồi phục.'
    WHERE BlogPostId = 2 AND (Title LIKE '%Nhá»•%' OR Title LIKE '%khÃ´n%');

    UPDATE BlogPosts
    SET Title = N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay',
        Summary = N'Mất răng không chỉ ảnh hưởng đến thẩm mỹ và chức năng ăn nhai mà còn dẫn đến tiêu xương hàm, lão hóa cơ mặt. Cấy ghép Implant là kỹ thuật hiện đại duy nhất phục hồi trọn vẹn cả chân răng và thân răng, mang lại cảm giác ăn nhai vững chắc như răng thật.'
    WHERE BlogPostId = 3 AND (Title LIKE '%Cáº¥y%' OR Title LIKE '%ghÃ©p%');

    UPDATE BlogPosts
    SET Title = N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?',
        Summary = N'Bạn đang phân vân giữa niềng răng mắc cài truyền thống và niềng răng khay trong suốt Invisalign? Bài viết phân tích chi tiết ưu nhược điểm, chi phí và hiệu quả của từng phương pháp giúp bạn dễ dàng đưa ra quyết định phù hợp nhất.'
    WHERE BlogPostId = 4 AND (Title LIKE '%Niá» ng%' OR Title LIKE '%mÃ¡ng%');

    UPDATE BlogPosts
    SET Title = N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp',
        Summary = N'Đánh răng 2 lần mỗi ngày đã thực sự đủ để bảo vệ hàm răng của bạn khỏi sâu răng và viêm nướu? Khám phá 6 thói quen chăm sóc răng miệng chuẩn nha khoa từ việc chải răng đúng kỹ thuật Bass đến sử dụng chỉ nha khoa và máy tăm nước.'
    WHERE BlogPostId = 5 AND (Title LIKE '%HÆ°á»›ng%' OR Title LIKE '%chÄƒm%');

    -- Post 6: Fix both mojibake and classic typo "Chuẩn Đoán" -> "Chẩn Đoán"
    UPDATE BlogPosts
    SET Title = N'Nha Khoa DentalCare chính thức khai trương Trung Tâm Chẩn Đoán Hình Ảnh & Laser 3D',
        Summary = N'Hệ thống máy chụp CT ConeBeam 3D thế hệ mới nhất cùng công nghệ Laser hiện đại đã chính thức đi vào hoạt động nhằm nâng cao chất lượng chẩn đoán và điều trị.'
    WHERE BlogPostId = 6 AND (Title LIKE '%Khai TrÆ°Æ¡ng%' OR Title LIKE '%Chuẩn Đoán%' OR Title LIKE '%chÃ­nh thá»©c%');

    -- Post 12: Restore post-extraction care guide
    UPDATE BlogPosts
    SET Title = N'Cẩm nang chăm sóc răng miệng sau khi nhổ răng khôn',
        Slug = 'cam-nang-cham-soc-rang-mieng-sau-khi-nho-rang-khon',
        Summary = N'Những lưu ý quan trọng cần biết trong 48 giờ đầu sau phẫu thuật nhổ răng khôn.',
        Content = N'<p>Sau khi nhổ răng khôn, bệnh nhân cần cắn chặt bông gòn trong 45 phút đầu và không súc miệng bằng nước muối đậm đặc...</p>'
    WHERE BlogPostId = 12 AND (Title LIKE '%Cáº©m%' OR Title LIKE '%chÄƒm%' OR Slug LIKE '%ca-m-nang%');
    PRINT '    [BlogPosts] Titles & Summaries restored: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- --------------------------------------------------------------------------------------------
    -- 4. DENTIST PROFILES
    -- --------------------------------------------------------------------------------------------
    PRINT '>>> 4. Correcting DentistProfiles...';

    UPDATE DentistProfiles
    SET Qualification = N'Tiến sĩ Răng Hàm Mặt, ĐHYD TP.HCM',
        Biography = N'Chuyên gia Cấy ghép Implant và Phẫu thuật hàm mặt kỹ thuật cao với hơn 15 năm kinh nghiệm lâm sàng.'
    WHERE UserId = 4 AND Qualification LIKE '%Tiáº¿n sÄ©%';

    UPDATE DentistProfiles
    SET Qualification = N'Bác sĩ CKII Răng Hàm Mặt',
        Biography = N'Chuyên gia Điều trị tủy và Phục hình nha khoa tổng quát chuyên sâu với hơn 18 năm kinh nghiệm.'
    WHERE UserId = 10 AND Qualification LIKE '%BÃ¡c sÄ©%';

    UPDATE DentistProfiles
    SET Qualification = N'Thạc sĩ Chỉnh nha Thẩm mỹ',
        Biography = N'Chuyên gia Niềng răng khay trong suốt Invisalign và Thẩm mỹ nụ cười toàn diện.'
    WHERE UserId = 11 AND Qualification LIKE '%Tháº¡c sÄ©%';

    UPDATE DentistProfiles
    SET Qualification = N'Thạc sĩ Nha khoa Trẻ em',
        Biography = N'Bác sĩ chuyên khoa Nha khoa Trẻ em và Tiền chỉnh nha can thiệp sớm cho thanh thiếu niên.'
    WHERE UserId = 12 AND Qualification LIKE '%Tháº¡c sÄ©%';
    PRINT '    [DentistProfiles] Qualifications and Biographies restored: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    -- --------------------------------------------------------------------------------------------
    -- 5. INVOICE ITEMS
    -- --------------------------------------------------------------------------------------------
    PRINT '>>> 5. Correcting InvoiceItems...';

    UPDATE InvoiceItems
    SET Description = N'Khám tổng quát & tư vấn chuyên khoa răng miệng'
    WHERE InvoiceItemId = 7 AND Description LIKE '%KhÃ¡m%';

    UPDATE InvoiceItems
    SET Description = N'Lấy cao răng & đánh bóng 2 hàm công nghệ siêu âm Cavitron'
    WHERE InvoiceItemId = 8 AND Description LIKE '%Láº¥y cao%';
    PRINT '    [InvoiceItems] Service descriptions restored: ' + CAST(@@ROWCOUNT AS VARCHAR) + ' rows.';

    COMMIT TRANSACTION;
    PRINT '================================================================================================';
    PRINT 'ALL CORRECTIONS APPLIED AND COMMITTED SUCCESSFULLY!';
    PRINT '================================================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR OCCURRED DURING CORRECTION: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
