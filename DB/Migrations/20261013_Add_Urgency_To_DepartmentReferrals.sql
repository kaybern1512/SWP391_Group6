-- ==============================================================================
-- MIGRATION SCRIPT: Add Urgency column to DepartmentReferrals
-- STATUS: PREPARED - PENDING APPROVAL (DO NOT EXECUTE AUTOMATICALLY)
-- Target: DepartmentReferrals table
-- Allowed Values: 'Normal', 'Urgent'
-- ==============================================================================

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('DepartmentReferrals')
      AND name = 'Urgency'
)
BEGIN
    PRINT 'Adding Urgency column to DepartmentReferrals...';

    ALTER TABLE DepartmentReferrals
    ADD Urgency VARCHAR(20) NOT NULL CONSTRAINT DF_DepartmentReferrals_Urgency DEFAULT 'Normal';

    ALTER TABLE DepartmentReferrals
    ADD CONSTRAINT CK_DepartmentReferrals_Urgency CHECK (Urgency IN ('Normal', 'Urgent'));

    PRINT 'Urgency column and CK_DepartmentReferrals_Urgency constraint added successfully.';
END
ELSE
BEGIN
    PRINT 'Urgency column already exists in DepartmentReferrals.';
END
GO
