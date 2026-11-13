-- ============================================================================
-- SCRIPT: Fix_Prescriptions_Vietnamese.sql
-- PURPOSE: Sửa triệt để lỗi font / mã hóa tiếng Việt cho Đơn thuốc (Prescriptions & PrescriptionItems)
-- Target: DentalClinicManagementDB
-- ============================================================================

USE DentalClinicManagementDB;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

BEGIN TRANSACTION;

-- 1. Sửa hướng dẫn chung cho đơn thuốc (Prescriptions)
UPDATE Prescriptions
SET GeneralInstructions = N'Uống thuốc sau bữa ăn no 15-30 phút. Uống nhiều nước, kiêng rượu bia và thuốc lá trong thời gian dùng thuốc.'
WHERE PrescriptionId = 3 AND (GeneralInstructions IS NULL OR GeneralInstructions = '');

UPDATE Prescriptions
SET GeneralInstructions = N'Uống thuốc đúng giờ sau ăn no. Cắn gạc chặt trong 60 phút. Không súc miệng nước muối mạnh hay khạc nhổ trong 24h đầu. Chườm lạnh má ngày đầu, chườm ấm ngày thứ 2.'
WHERE PrescriptionId = 4;

-- 2. Sửa chi tiết các loại thuốc trong đơn (PrescriptionItems)
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

-- Cập nhật bổ sung cho các dòng có thể bị lỗi ký tự trong toàn bộ PrescriptionItems
UPDATE PrescriptionItems
SET Dosage = REPLACE(REPLACE(REPLACE(REPLACE(Dosage, 'viÃªn', N'viên'), 'lÃ n', N'lần'), 'ngÃ y', N'ngày'), 'tiáº¿ng', N'tiếng'),
    Frequency = REPLACE(REPLACE(REPLACE(REPLACE(Frequency, 'viÃªn', N'viên'), 'lÃ n', N'lần'), 'ngÃ y', N'ngày'), 'tiáº¿ng', N'tiếng'),
    Duration = REPLACE(REPLACE(REPLACE(REPLACE(Duration, 'viÃªn', N'viên'), 'lÃ n', N'lần'), 'ngÃ y', N'ngày'), 'tiáº¿ng', N'tiếng'),
    Instructions = REPLACE(REPLACE(REPLACE(REPLACE(Instructions, 'Uá»‘ng', N'Uống'), 'sÃ¡ng', N'sáng'), 'tá»‘i', N'tối'), 'Ä‘au', N'đau')
WHERE Dosage LIKE '%Ã%' OR Frequency LIKE '%Ã%' OR Duration LIKE '%Ã%' OR Instructions LIKE '%Ã%';

COMMIT TRANSACTION;
GO
