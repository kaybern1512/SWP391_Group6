-- ================================================================================================
-- DENTALCARE - VIETNAMESE DATA QUALITY CORRECTIONS (ROLLBACK SCRIPT)
-- Database Target: DentalCare_Integration_Dev_QA_Audit (or DentalCare_Integration_Dev)
-- Description: Reverts the forward corrections back to previous state if needed.
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @TargetDB NVARCHAR(128) = DB_NAME();
PRINT '================================================================================================';
PRINT 'STARTING ROLLBACK OF VIETNAMESE DATA QUALITY CORRECTIONS ON: ' + @TargetDB;
PRINT 'TIME: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '================================================================================================';

BEGIN TRANSACTION;

BEGIN TRY
    -- 1. Rollback DepartmentServices
    UPDATE DepartmentServices
    SET ServiceName = N'Khám nha khoa trẻ em & Bôi Flour ngừa sâu'
    WHERE DepartmentServiceId = 10 AND ServiceName = N'Khám nha khoa trẻ em & Bôi Fluor ngừa sâu';

    UPDATE DepartmentServices
    SET BookingInstructions = N'Vui lÃ²ng Ä‘áº¿n trÆ°á»›c 10 phÃºt Ä‘á»ƒ hoÃ n táº¥t thá»§ tá»¥c tiáº¿p Ä‘Ã³n vÃ  chá»¥p phim cÆ¡ báº£n náº¿u cáº§n.'
    WHERE DepartmentServiceId IN (1, 5, 10);

    UPDATE DepartmentServices
    SET BookingInstructions = N'Thá»§ thuáº­t chuyÃªn sÃ¢u cáº§n khÃ¡m Ä‘Ã¡nh giÃ¡ trÆ°á»›c. Vui lÃ²ng Äƒn nháº¹ trÆ°á»›c háº¹n 1 tiáº¿ng, khÃ´ng uá»‘ng cÃ  phÃª hoáº·c cháº¥t kÃ­ch thÃ­ch.'
    WHERE DepartmentServiceId IN (8, 9, 21, 22, 23, 24, 25);

    UPDATE DepartmentServices
    SET BookingInstructions = N'Dá»‹ch vá»¥ nha khoa tráº» em nháº¹ nhÃ ng. Ba máº¹ vui lÃ²ng Ä‘á»™ng viÃªn tÃ¢m lÃ½ bÃ© trÆ°á»›c khi tá»›i phÃ²ng khÃ¡m.'
    WHERE DepartmentServiceId IN (26, 27, 28, 29, 30, 31);

    -- 2. Rollback Medicines
    UPDATE Medicines SET DosageForm = 'ViÃªn nang' WHERE MedicineId = 1;
    UPDATE Medicines SET DosageForm = 'ViÃªn nÃ©n bao phim' WHERE MedicineId IN (2, 6);
    UPDATE Medicines SET DosageForm = 'ViÃªn nÃ©n' WHERE MedicineId IN (3, 5);
    UPDATE Medicines SET DosageForm = 'ViÃªn sá»§i' WHERE MedicineId = 4;
    UPDATE Medicines SET DosageForm = 'ViÃªn ngáº­m' WHERE MedicineId = 7;
    UPDATE Medicines SET DosageForm = 'Dung dá»‹ch sÃºc miá»‡ng' WHERE MedicineId = 8;
    UPDATE Medicines SET MedicineName = 'NÆ°á»›c sÃºc miá»‡ng Chlorhexidine 0.12%' WHERE MedicineId = 8;

    -- 3. Rollback DentistProfiles
    UPDATE DentistProfiles SET Qualification = 'Tiáº¿n sÄ© RÄƒng HÃ m Máº·t, Ä HYD TP.HCM', Biography = 'ChuyÃªn gia Cáº¥y ghÃ©p Implant vÃ  Pháº«u thuáº­t hÃ m máº·t ká»¹ thuáº­t cao vá»›i hÆ¡n 15 nÄƒm kinh nghiá»‡m lÃ¢m sÃ ng.' WHERE UserId = 4;
    UPDATE DentistProfiles SET Qualification = 'BÃ¡c sÄ© CKII RÄƒng HÃ m Máº·t', Biography = 'ChuyÃªn gia Ä iá» u trá»‹ tá»§y vÃ  Phá»¥c hÃ¬nh nha khoa tá»•ng quÃ¡t chuyÃªn sÃ¢u vá»›i hÆ¡n 18 nÄƒm kinh nghiá»‡m.' WHERE UserId = 10;
    UPDATE DentistProfiles SET Qualification = 'Tháº¡c sÄ© Chá»‰nh nha Tháº©m má»¹', Biography = 'ChuyÃªn gia Niá» ng rÄƒng khay trong suá»‘t Invisalign vÃ  Tháº©m má»¹ ná»¥ cÆ°á» i toÃ n diá»‡n.' WHERE UserId = 11;
    UPDATE DentistProfiles SET Qualification = 'Tháº¡c sÄ© Nha khoa Tráº» em', Biography = 'BÃ¡c sÄ© chuyÃªn khoa Nha khoa Tráº» em vÃ  Tiá» n chá»‰nh nha can thiá»‡p sá»›m cho thanh thiáº¿u niÃªn.' WHERE UserId = 12;

    -- 4. Rollback InvoiceItems
    UPDATE InvoiceItems SET Description = 'KhÃ¡m tá»•ng quÃ¡t & tÆ° váº¥n chuyÃªn khoa rÄƒng miá»‡ng' WHERE InvoiceItemId = 7;
    UPDATE InvoiceItems SET Description = 'Láº¥y cao rÄƒng & Ä‘Ã¡nh bÃ³ng 2 hÃ m cÃ´ng nghá»‡ siÃªu Ã¢m Cavitron' WHERE InvoiceItemId = 8;

    -- 5. Rollback BlogPosts
    UPDATE BlogPosts
    SET Title = 'Cáº©m nang chÄƒm sÃ³c rÄƒng miá»‡ng sau khi nhá»• rÄƒng khÃ´n',
        Slug = 'ca-m-nang-cha-m-sa-c-ra-ng-mia-ng-sau-khi-nha-ra-ng-kha-n',
        Summary = 'Nhá»¯ng lÆ°u Ã½ quan trá» ng cáº§n biáº¿t trong 48 giá»  Ä‘áº§u sau pháº«u thuáº­t nhá»• rÄƒng khÃ´n.'
    WHERE BlogPostId = 12;

    COMMIT TRANSACTION;
    PRINT 'ROLLBACK COMPLETED SUCCESSFULLY.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR DURING ROLLBACK: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
