# ============================================================================
# SCRIPT: Auto_Fix_All_Database_Mojikake.ps1
# MỤC ĐÍCH: Quét tự động 100% các bảng và cột chuỗi trong database DentalClinicManagementDB
#           và giải mã triệt để toàn bộ lỗi font / Mojikake (CP1252 / CP1258 -> UTF-8).
# ============================================================================

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$charToByte = @{
    [char]0x0102 = [byte]0xC3  # Ă -> 0xC3
    [char]0x00C2 = [byte]0xC2  # Â -> 0xC2
    [char]0x01AF = [byte]0xC6  # Ư -> 0xC6
    [char]0x2030 = [byte]0x89  # ‰ -> 0x89
    [char]0x2018 = [byte]0x91  # ‘ -> 0x91
    [char]0x2019 = [byte]0x92  # ’ -> 0x92
    [char]0x201C = [byte]0x93  # “ -> 0x93
    [char]0x201D = [byte]0x94  # ” -> 0x94
    [char]0x2022 = [byte]0x95  # • -> 0x95
    [char]0x2013 = [byte]0x96  # – -> 0x96
    [char]0x2014 = [byte]0x97  # — -> 0x97
    [char]0x02DC = [byte]0x98  # ˜ -> 0x98
    [char]0x2122 = [byte]0x99  # ™ -> 0x99
    [char]0x0161 = [byte]0x9A  # š -> 0x9A
    [char]0x203A = [byte]0x9B  # › -> 0x9B
    [char]0x0153 = [byte]0x9C  # œ -> 0x9C
    [char]0x017E = [byte]0x9E  # ž -> 0x9E
    [char]0x0178 = [byte]0x9F  # Ÿ -> 0x9F
    [char]0x20AC = [byte]0x80  # € -> 0x80
    [char]0x201A = [byte]0x82  # ‚ -> 0x82
    [char]0x0192 = [byte]0x83  # ƒ -> 0x83
    [char]0x201E = [byte]0x84  # „ -> 0x84
    [char]0x2026 = [byte]0x85  # … -> 0x85
    [char]0x2020 = [byte]0x86  # † -> 0x86
    [char]0x2021 = [byte]0x87  # ‡ -> 0x87
    [char]0x02C6 = [byte]0x88  # ˆ -> 0x88
    [char]0x0160 = [byte]0x8A  # Š -> 0x8A
    [char]0x2039 = [byte]0x8B  # ‹ -> 0x8B
    [char]0x0152 = [byte]0x8C  # Œ -> 0x8C
    [char]0x017D = [byte]0x8E  # Ž -> 0x8E
}

function Decode-Chunk($str) {
    if ([string]::IsNullOrEmpty($str)) { return $str }
    $bytes = [System.Collections.Generic.List[byte]]::new()
    foreach ($ch in $str.ToCharArray()) {
        if ($charToByte.ContainsKey($ch)) {
            $bytes.Add($charToByte[$ch])
        } elseif ([int][char]$ch -le 255) {
            $bytes.Add([byte][char]$ch)
        } else {
            return $str
        }
    }
    try {
        $decoded = [System.Text.Encoding]::UTF8.GetString($bytes.ToArray())
        if ($decoded.Contains([char]65533)) { return $str }
        return $decoded
    } catch {
        return $str
    }
}

function Fix-Text($text) {
    if ([string]::IsNullOrEmpty($text)) { return $text }
    
    # 1. Try decoding the whole string first
    $whole = Decode-Chunk $text
    if ($whole -ne $text -and -not $whole.Contains([char]65533)) {
        return $whole
    }

    # 2. Process token by token
    $lines = $text -split "(?m)(?=[\r\n])|(?<=[\r\n])"
    $fixedLines = [System.Collections.Generic.List[string]]::new()
    $anyChanged = $false

    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) {
            $fixedLines.Add($line)
            continue
        }
        $words = $line -split " "
        $fixedWords = [System.Collections.Generic.List[string]]::new()
        foreach ($w in $words) {
            if ([string]::IsNullOrEmpty($w)) {
                $fixedWords.Add($w)
                continue
            }
            # Separate leading/trailing punctuation
            $m = [regex]::Match($w, "^([\p{P}\p{S}]*)(.*?)([\p{P}\p{S}]*)$")
            if ($m.Success) {
                $prefix = $m.Groups[1].Value
                $core = $m.Groups[2].Value
                $suffix = $m.Groups[3].Value
                $decCore = Decode-Chunk $core
                if ($decCore -ne $core -and -not $decCore.Contains([char]65533)) {
                    $fixedWords.Add($prefix + $decCore + $suffix)
                    $anyChanged = $true
                } else {
                    $decW = Decode-Chunk $w
                    if ($decW -ne $w -and -not $decW.Contains([char]65533)) {
                        $fixedWords.Add($decW)
                        $anyChanged = $true
                    } else {
                        $fixedWords.Add($w)
                    }
                }
            } else {
                $dec = Decode-Chunk $w
                if ($dec -ne $w -and -not $dec.Contains([char]65533)) {
                    $fixedWords.Add($dec)
                    $anyChanged = $true
                } else {
                    $fixedWords.Add($w)
                }
            }
        }
        $fixedLines.Add(($fixedWords -join " "))
    }

    if ($anyChanged) {
        return ($fixedLines -join "")
    }
    return $text
}

$serverInstance = ".\MSSQLSERVER01"
$connStr = "Server=$serverInstance;Database=DentalClinicManagementDB;Trusted_Connection=True;TrustServerCertificate=True;"
$con = New-Object System.Data.SqlClient.SqlConnection($connStr)
$con.Open()

# Get list of tables and primary keys
$pkCmd = $con.CreateCommand()
$pkCmd.CommandText = @"
SELECT 
    t.name AS TableName,
    c.name AS PkColumn
FROM sys.tables t
JOIN sys.indexes i ON t.object_id = i.object_id AND i.is_primary_key = 1
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE t.name NOT IN ('__EFMigrationsHistory', 'AuditLogs')
"@
$pkAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($pkCmd)
$pkDt = New-Object System.Data.DataTable
$pkAdapter.Fill($pkDt) | Out-Null

$pkMap = @{}
foreach ($row in $pkDt.Rows) {
    $tbl = $row["TableName"].ToString()
    $col = $row["PkColumn"].ToString()
    if (-not $pkMap.ContainsKey($tbl)) {
        $pkMap[$tbl] = $col
    }
}

# Get all string columns
$colCmd = $con.CreateCommand()
$colCmd.CommandText = @"
SELECT t.name AS TableName, c.name AS ColumnName
FROM sys.tables t
JOIN sys.columns c ON t.object_id = c.object_id
JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE ty.name IN ('nvarchar', 'varchar', 'text', 'ntext')
  AND t.name NOT IN ('__EFMigrationsHistory', 'AuditLogs')
ORDER BY t.name, c.name
"@
$colAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($colCmd)
$colDt = New-Object System.Data.DataTable
$colAdapter.Fill($colDt) | Out-Null

$totalRepaired = 0

foreach ($row in $colDt.Rows) {
    $tbl = $row["TableName"].ToString()
    $col = $row["ColumnName"].ToString()
    if (-not $pkMap.ContainsKey($tbl)) { continue }
    $pk = $pkMap[$tbl]

    $qCmd = $con.CreateCommand()
    $qCmd.CommandText = @"
SELECT [$pk], [$col]
FROM [$tbl]
WHERE [$col] IS NOT NULL
  AND (
       [$col] COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(195) + N'%'
    OR [$col] COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(196) + N'%'
    OR [$col] COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(197) + N'%'
    OR [$col] COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(198) + N'%'
    OR [$col] COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(225) + N'%'
    OR [$col] COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(258) + N'%'
  )
"@

    $rAdapter = New-Object System.Data.SqlClient.SqlDataAdapter($qCmd)
    $rDt = New-Object System.Data.DataTable
    try {
        $rAdapter.Fill($rDt) | Out-Null
        if ($rDt.Rows.Count -gt 0) {
            Write-Host "Đang xử lý [$tbl].[$col] ($($rDt.Rows.Count) bản ghi)..."
            foreach ($rRow in $rDt.Rows) {
                $pkVal = $rRow[$pk]
                $badVal = $rRow[$col].ToString()
                $fixedVal = Fix-Text $badVal
                if ($fixedVal -ne $badVal) {
                    $uCmd = $con.CreateCommand()
                    $uCmd.CommandText = "UPDATE [$tbl] SET [$col] = @fixed WHERE [$pk] = @pkVal"
                    $p1 = $uCmd.Parameters.Add("@fixed", [System.Data.SqlDbType]::NVarChar, -1)
                    $p1.Value = $fixedVal
                    $p2 = $uCmd.Parameters.Add("@pkVal", [System.Data.SqlDbType]::Variant)
                    $p2.Value = $pkVal
                    $uCmd.ExecuteNonQuery() | Out-Null
                    $totalRepaired++
                }
            }
        }
    } catch {
        Write-Host "Lỗi xử lý [$tbl].[$col]: $_"
    }
}

Write-Host "============================================="
Write-Host "HOÀN TẤT! Đã sửa triệt để: $totalRepaired trường dữ liệu."
Write-Host "============================================="
$con.Close()
