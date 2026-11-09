$ErrorActionPreference = "Stop"
$connStr = "Server=localhost\SQLEXPRESS;Database=DentalCare_Integration_Dev;Trusted_Connection=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

$dumpDir = "D:\SWP391\DentalCare-Integration\DB\data_quality\data_dump"
if (-not (Test-Path $dumpDir)) {
    New-Item -ItemType Directory -Force -Path $dumpDir | Out-Null
}

# Tables to dump and inspect
$tables = @(
    @{ Table = "Departments"; PK = "DepartmentId"; Cols = @("DepartmentId", "Name", "Description", "Status") },
    @{ Table = "DepartmentServices"; PK = "DepartmentServiceId"; Cols = @("DepartmentServiceId", "DepartmentId", "ServiceName", "Description", "DurationMinutes", "Price", "Status", "ServiceType", "BookingInstructions") },
    @{ Table = "Medicines"; PK = "MedicineId"; Cols = @("MedicineId", "MedicineCode", "MedicineName", "ActiveIngredient", "Concentration", "DosageForm", "Unit", "UsageInstructions", "Status") },
    @{ Table = "MedicineBatches"; PK = "MedicineBatchId"; Cols = @("MedicineBatchId", "MedicineId", "BatchNumber", "Status") },
    @{ Table = "MedicineDispensations"; PK = "DispensationId"; Cols = @("DispensationId", "AppointmentId", "Notes", "Status") },
    @{ Table = "MedicineStockTransactions"; PK = "TransactionId"; Cols = @("TransactionId", "MedicineBatchId", "TransactionType", "Reason", "ReferenceCode") },
    @{ Table = "ClinicProfile"; PK = "ClinicId"; Cols = @("ClinicId", "Name", "Address", "PhoneNumber", "Email", "Description") },
    @{ Table = "Rooms"; PK = "RoomId"; Cols = @("RoomId", "RoomCode", "RoomName", "DepartmentId", "Status") },
    @{ Table = "DentalChairs"; PK = "DentalChairId"; Cols = @("DentalChairId", "ChairCode", "RoomId", "Status") },
    @{ Table = "BlogCategories"; PK = "CategoryId"; Cols = @("CategoryId", "Name", "Slug") },
    @{ Table = "BlogPosts"; PK = "BlogPostId"; Cols = @("BlogPostId", "Title", "Slug", "Summary", "Content", "PostType", "Status", "RejectionReason") },
    @{ Table = "Notifications"; PK = "NotificationId"; Cols = @("NotificationId", "Type", "Channel", "Subject", "Content", "DeliveryStatus") },
    @{ Table = "DentistProfiles"; PK = "UserId"; Cols = @("UserId", "LicenseNumber", "Qualification", "Biography") },
    @{ Table = "StaffProfiles"; PK = "UserId"; Cols = @("UserId", "EmployeeCode", "FullName", "EmploymentStatus") },
    @{ Table = "AppointmentReminderJobs"; PK = "ReminderJobId"; Cols = @("ReminderJobId", "ReminderType", "RecipientEmail", "Status", "ErrorMessage") },
    @{ Table = "Appointments"; PK = "AppointmentId"; Cols = @("AppointmentId", "AppointmentCode", "ReasonForVisit", "BookingSource", "QueueNumber", "Status", "CancellationReason") },
    @{ Table = "AppointmentChangeProposals"; PK = "ProposalId"; Cols = @("ProposalId", "AppointmentId", "Reason", "Status") },
    @{ Table = "DepartmentReferrals"; PK = "ReferralId"; Cols = @("ReferralId", "Reason", "Status", "Urgency", "ClinicalInstructions") },
    @{ Table = "FollowUpSchedules"; PK = "FollowUpId"; Cols = @("FollowUpId", "Reason", "Status") },
    @{ Table = "ClinicalEntries"; PK = "ClinicalEntryId"; Cols = @("ClinicalEntryId", "Symptoms", "ClinicalFindings", "Diagnosis", "DentalConditions", "ClinicalNotes") },
    @{ Table = "OdontogramEntries"; PK = "OdontogramEntryId"; Cols = @("OdontogramEntryId", "ToothNumber", "Surface", "Condition", "Note") },
    @{ Table = "TreatmentPlanItems"; PK = "TreatmentPlanItemId"; Cols = @("TreatmentPlanItemId", "ToothNumber", "Status", "Note") },
    @{ Table = "TreatmentSessions"; PK = "TreatmentSessionId"; Cols = @("TreatmentSessionId", "TreatmentResult", "ClinicalNote") },
    @{ Table = "PrescriptionItems"; PK = "PrescriptionItemId"; Cols = @("PrescriptionItemId", "MedicationName", "Dosage", "Frequency", "Duration", "Instructions") },
    @{ Table = "Prescriptions"; PK = "PrescriptionId"; Cols = @("PrescriptionId", "GeneralInstructions") },
    @{ Table = "VisitFeedback"; PK = "FeedbackId"; Cols = @("FeedbackId", "Comment", "Status", "AdminNotes", "AdminResponse") },
    @{ Table = "Invoices"; PK = "InvoiceId"; Cols = @("InvoiceId", "InvoiceCode", "Status") },
    @{ Table = "InvoiceItems"; PK = "InvoiceItemId"; Cols = @("InvoiceItemId", "Description") },
    @{ Table = "SupportChatMessages"; PK = "MessageId"; Cols = @("MessageId", "SenderType", "SenderName", "MessageText") }
)

$summary = @()
$totalRecords = 0

foreach ($t in $tables) {
    $tName = $t.Table
    $cols = $t.Cols -join ", "
    $sql = "SELECT $cols FROM [$tName]"
    
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $tableData = New-Object System.Data.DataTable
    $adapter.Fill($tableData) | Out-Null
    
    $count = $tableData.Rows.Count
    $totalRecords += $count
    
    # Convert DataTable to array of hashtables
    $rows = @()
    foreach ($row in $tableData.Rows) {
        $item = [ordered]@{}
        foreach ($col in $tableData.Columns) {
            $val = $row[$col.ColumnName]
            if ($val -is [System.DBNull]) {
                $item[$col.ColumnName] = $null
            } else {
                $item[$col.ColumnName] = $val
            }
        }
        $rows += $item
    }
    
    $json = $rows | ConvertTo-Json -Depth 5
    $filePath = Join-Path $dumpDir "$tName.json"
    [System.IO.File]::WriteAllText($filePath, $json, [System.Text.Encoding]::UTF8)
    
    $summary += [PSCustomObject]@{
        Table = $tName
        PK = $t.PK
        ColumnCount = $t.Cols.Count
        RowCount = $count
    }
    Write-Host ("Exported {0,-26} : {1,5} rows" -f $tName, $count)
}

$conn.Close()
Write-Host "----------------------------------------------------"
Write-Host "Total Tables Exported : $($tables.Count)"
Write-Host "Total Records Dumped  : $totalRecords"
