IF COL_LENGTH('dbo.DepartmentReferrals', 'Urgency') IS NULL
BEGIN
    ALTER TABLE dbo.DepartmentReferrals
        ADD Urgency VARCHAR(20) NOT NULL
            CONSTRAINT DF_DepartmentReferrals_Urgency DEFAULT 'Normal'
            CONSTRAINT CK_DepartmentReferrals_Urgency CHECK (Urgency IN ('Normal', 'Urgent'));
END;
GO
