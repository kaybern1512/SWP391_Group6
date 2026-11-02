USE DentalClinicManagementDB;
SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#MojikakeReport') IS NOT NULL DROP TABLE #MojikakeReport;
CREATE TABLE #MojikakeReport (
    TableName NVARCHAR(128),
    ColumnName NVARCHAR(128),
    BadRowCount INT,
    SampleValue NVARCHAR(500)
);

DECLARE @tbl NVARCHAR(128), @col NVARCHAR(128), @sql NVARCHAR(MAX);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name, c.name
FROM sys.tables t
JOIN sys.columns c ON t.object_id = c.object_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE ty.name IN ('nvarchar', 'varchar')
  AND t.name NOT IN ('__EFMigrationsHistory', 'AuditLogs')
ORDER BY t.name, c.name;

OPEN cur;
FETCH NEXT FROM cur INTO @tbl, @col;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'
    DECLARE @c INT;
    SELECT @c = COUNT(*) FROM [' + @tbl + '] 
    WHERE [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%Ã%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%á»%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%áº%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%Ä%''
       OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%Æ%'';
       
    IF @c > 0
    BEGIN
        INSERT INTO #MojikakeReport (TableName, ColumnName, BadRowCount, SampleValue)
        SELECT TOP 1 ''' + @tbl + ''', ''' + @col + ''', @c, SUBSTRING(CAST([' + @col + '] AS NVARCHAR(MAX)), 1, 300)
        FROM [' + @tbl + ']
        WHERE [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%Ã%''
           OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%á»%''
           OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%áº%''
           OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%Ä%''
           OR [' + @col + '] COLLATE Latin1_General_BIN LIKE N''%Æ%'';
    END;';

    BEGIN TRY
        EXEC sp_executesql @sql;
    END TRY
    BEGIN CATCH
    END CATCH;

    FETCH NEXT FROM cur INTO @tbl, @col;
END;

CLOSE cur;
DEALLOCATE cur;

SELECT TableName, ColumnName, BadRowCount, SampleValue FROM #MojikakeReport ORDER BY TableName, ColumnName;
DROP TABLE #MojikakeReport;
