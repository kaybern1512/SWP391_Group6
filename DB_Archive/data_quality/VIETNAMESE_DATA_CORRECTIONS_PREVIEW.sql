-- ================================================================================================
-- DENTALCARE - VIETNAMESE DATA QUALITY CORRECTIONS PREVIEW SCRIPT
-- Database: DentalCare_Integration_Dev (or DentalCare_Integration_Dev_QA_Audit)
-- Description: Read-only preview script comparing current data with proposed standardized values.
-- ================================================================================================

USE DentalCare_Integration_Dev;
GO

PRINT '================================================================================================';
PRINT '1. PREVIEW: DEPARTMENT SERVICES (BOOKING INSTRUCTIONS & SERVICE NAMES)';
PRINT '================================================================================================';

SELECT 
    DepartmentServiceId,
    ServiceName,
    CASE 
        WHEN ServiceName LIKE '%Flour%' THEN REPLACE(ServiceName, 'Flour', 'Fluor')
        ELSE ServiceName 
    END AS ProposedServiceName,
    BookingInstructions AS CurrentBookingInstructions,
    CASE 
        WHEN BookingInstructions LIKE '%Vui l%' 
            THEN N'Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.'
        WHEN BookingInstructions LIKE '%chuy%' 
            THEN N'Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.'
        WHEN BookingInstructions LIKE '%nha khoa tr%' 
            THEN N'Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám.'
        ELSE BookingInstructions
    END AS ProposedBookingInstructions
FROM DepartmentServices
WHERE BookingInstructions LIKE '%[%' 
   OR BookingInstructions LIKE '%Ã%' 
   OR BookingInstructions LIKE '%Ä%' 
   OR BookingInstructions LIKE '%á»%'
   OR ServiceName LIKE '%Flour%';

PRINT '================================================================================================';
PRINT '2. PREVIEW: MEDICINES (UNIT, DOSAGE FORM, USAGE INSTRUCTIONS)';
PRINT '================================================================================================';

SELECT 
    MedicineId,
    MedicineCode,
    MedicineName,
    Unit AS CurrentUnit,
    N'Viên' AS ProposedUnit,
    DosageForm AS CurrentDosageForm,
    CASE 
        WHEN DosageForm = 'ViÃªn nang' THEN N'Viên nang'
        WHEN DosageForm = 'ViÃªn nÃ©n' THEN N'Viên nén'
        WHEN DosageForm = 'ViÃªn nÃ©n bao phim' THEN N'Viên nén bao phim'
        WHEN DosageForm = 'ViÃªn sá»§i' THEN N'Viên sủi'
        WHEN DosageForm LIKE '%ngáº%' THEN N'Viên ngậm'
        WHEN DosageForm LIKE '%Dung dá»‹ch%' THEN N'Dung dịch súc miệng'
        ELSE DosageForm
    END AS ProposedDosageForm,
    UsageInstructions AS CurrentUsageInstructions
FROM Medicines
WHERE Unit LIKE '%Ã%' 
   OR DosageForm LIKE '%Ã%' 
   OR DosageForm LIKE '%á»%'
   OR UsageInstructions LIKE '%Ã%' 
   OR UsageInstructions LIKE '%Ä%' 
   OR UsageInstructions LIKE '%á»%'
   OR MedicineName LIKE '%NÆ°á»›c%';

PRINT '================================================================================================';
PRINT '3. PREVIEW: BLOG POSTS (TITLES, SUMMARIES, CONTENT)';
PRINT '================================================================================================';

SELECT 
    BlogPostId,
    Title AS CurrentTitle,
    CASE BlogPostId
        WHEN 1 THEN N'Điều trị viêm tủy răng như thế nào? Dấu hiệu và quy trình chuẩn y khoa'
        WHEN 2 THEN N'Nhổ răng khôn mọc lệch khi nào? Quy trình nhổ răng không đau bằng công nghệ Piezotome'
        WHEN 3 THEN N'Cấy ghép Implant nha khoa: Giải pháp phục hồi răng đã mất hoàn hảo nhất hiện nay'
        WHEN 4 THEN N'Niềng răng mắc cài hay máng trong suốt Invisalign: Nên chọn phương pháp nào?'
        WHEN 5 THEN N'Hướng dẫn chăm sóc răng miệng đúng cách: 6 thói quen bảo vệ nụ cười khỏe đẹp'
        WHEN 6 THEN N'Nha Khoa DentalCare chính thức khai trương Trung Tâm Chẩn Đoán Hình Ảnh & Laser 3D'
        WHEN 12 THEN N'Cẩm nang chăm sóc răng miệng sau khi nhổ răng khôn'
        ELSE Title
    END AS ProposedTitle
FROM BlogPosts
WHERE Title LIKE '%Ã%' 
   OR Title LIKE '%Ä%' 
   OR Title LIKE '%á»%'
   OR Title LIKE '%Chuẩn Đoán%';

PRINT '================================================================================================';
PRINT '4. PREVIEW: DENTIST PROFILES (QUALIFICATIONS & BIOGRAPHIES)';
PRINT '================================================================================================';

SELECT 
    UserId,
    LicenseNumber,
    Qualification AS CurrentQualification,
    CASE UserId
        WHEN 4 THEN N'Tiến sĩ Răng Hàm Mặt, ĐHYD TP.HCM'
        WHEN 10 THEN N'Bác sĩ CKII Răng Hàm Mặt'
        WHEN 11 THEN N'Thạc sĩ Chỉnh nha Thẩm mỹ'
        WHEN 12 THEN N'Thạc sĩ Nha khoa Trẻ em'
        ELSE Qualification
    END AS ProposedQualification
FROM DentistProfiles
WHERE Qualification LIKE '%Ã%' 
   OR Qualification LIKE '%Ä%' 
   OR Qualification LIKE '%á»%';

PRINT '================================================================================================';
PRINT '5. PREVIEW: INVOICE ITEMS (SERVICE DESCRIPTIONS)';
PRINT '================================================================================================';

SELECT 
    InvoiceItemId,
    Description AS CurrentDescription,
    CASE 
        WHEN Description LIKE '%KhÃ¡m%' THEN N'Khám tổng quát & tư vấn chuyên khoa răng miệng'
        WHEN Description LIKE '%Láº¥y cao%' THEN N'Lấy cao răng & đánh bóng 2 hàm công nghệ siêu âm Cavitron'
        ELSE Description
    END AS ProposedDescription
FROM InvoiceItems
WHERE Description LIKE '%Ã%' 
   OR Description LIKE '%Ä%' 
   OR Description LIKE '%á»%';
GO
