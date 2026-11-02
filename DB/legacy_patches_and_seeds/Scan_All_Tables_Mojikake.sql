USE DentalClinicManagementDB;
GO

SET NOCOUNT ON;

DECLARE @sql NVARCHAR(MAX) = N'';
DECLARE @tbl NVARCHAR(128), @col NVARCHAR(128);

IF OBJECT_ID('tempdb..#Results') IS NOT NULL DROP TABLE #Results;
CREATE TABLE #Results (
    TableName NVARCHAR(128),
    ColumnName NVARCHAR(128),
    IssueCount INT,
    SampleValue NVARCHAR(500)
);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name, c.name
FROM sys.tables t
JOIN sys.columns c ON t.object_id = c.object_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE ty.name IN ('nvarchar', 'varchar')
  AND t.name NOT IN ('__EFMigrationsHistory')
ORDER BY t.name, c.name;

OPEN cur;
FETCH NEXT FROM cur INTO @tbl, @col;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
    INSERT INTO #Results (TableName, ColumnName, IssueCount, SampleValue)
    SELECT TOP 1 ''' + @tbl + ''', ''' + @col + ''', COUNT(*) OVER(), CAST([' + @col + '] AS NVARCHAR(500))
    FROM [' + @tbl + ']
    WHERE [' + @col + '] COLLATE Latin1_General_BIN LIKE ''%Ã%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE ''%á»%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE ''%áº%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE ''%Ä%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE ''%Æ%''
       OR [' + @col + '] LIKE ''%???%'';';
    
    BEGIN TRY
        EXEC sp_executesql @sql;
    END TRY
    BEGIN CATCH
    END CATCH;

    FETCH NEXT FROM cur INTO @tbl, @col;
END;

CLOSE cur;
DEALLOCATE cur;

SELECT TableName, ColumnName, IssueCount, SampleValue FROM #Results ORDER BY TableName, ColumnName;
DROP TABLE #Results;
GO
