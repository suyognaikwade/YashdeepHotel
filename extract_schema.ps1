# Database Schema Extraction - PowerShell (Windows)
# Requires: 32-bit Microsoft Access Database Engine (ACE.OLEDB.12.0)
# Run in 32-bit PowerShell: C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe

param(
    [Parameter(Mandatory=$true)]
    [string]$MdbPath = "C:\xampp\htdocs\AntigravityProjects\YashdeepHotelMS\RSS26\dinurss.mdb",
    
    [string]$Password = $env:MDB_PASSWORD,
    
    [string]$OutputDir = ".\extracted_schema_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Extracting schema from: $MdbPath" -ForegroundColor Cyan
Write-Host "Output directory: $OutputDir" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (-not (Test-Path $MdbPath)) {
    Write-Error "File not found: $MdbPath"
    exit 1
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Connection string for password-protected MDB
$connStr = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$MdbPath;Jet OLEDB:Database Password=$Password;"

try {
    $conn = New-Object System.Data.OleDb.OleDbConnection($connStr)
    $conn.Open()
    
    Write-Host "Connected successfully!" -ForegroundColor Green
    
    # Get schema tables
    $schemaTables = $conn.GetSchema("Tables")
    
    # Filter user tables (not system tables)
    $userTables = $schemaTables | Where-Object { 
        $_.TABLE_TYPE -eq 'TABLE' -and 
        $_.TABLE_NAME -notlike 'MSys*' -and
        $_.TABLE_NAME -notlike '~*' 
    }
    
    Write-Host "`nFound $($userTables.Count) user tables:" -ForegroundColor Yellow
    
    $tableList = @()
    $rowCounts = @()
    
    foreach ($tableRow in $userTables) {
        $tableName = $tableRow.TABLE_NAME
        $tableList += $tableName
        
        # Get row count
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT COUNT(*) FROM [$tableName]"
        $count = $cmd.ExecuteScalar()
        
        $rowCounts += [PSCustomObject]@{
            Table = $tableName
            Rows = $count
        }
        
        Write-Host "  $tableName : $count rows"
    }
    
    # Save table list
    $tableList | Out-File "$OutputDir\tables.txt"
    
    # Save row counts
    $rowCounts | Export-Csv "$OutputDir\row_counts.csv" -NoTypeInformation
    
    # Export each table to CSV
    Write-Host "`nExporting table data..." -ForegroundColor Yellow
    
    foreach ($tableName in $tableList) {
        $safeName = $tableName.Replace('/', '_').Replace('\', '_').Replace(':', '_')
        $csvPath = "$OutputDir\$safeName.csv"
        
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT * FROM [$tableName]"
        
        $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
        $dt = New-Object System.Data.DataTable
        $adapter.Fill($dt) | Out-Null
        
        $dt | Export-Csv $csvPath -NoTypeInformation -Encoding UTF8
        Write-Host "  Exported: $tableName ($($dt.Rows.Count) rows)"
    }
    
    # Generate schema SQL (basic CREATE TABLE statements)
    Write-Host "`nGenerating schema SQL..." -ForegroundColor Yellow
    
    $schemaSql = @()
    $schemaSql += "-- Generated from $MdbPath"
    $schemaSql += "-- Date: $(Get-Date)"
    $schemaSql += "-- Tables: $($userTables.Count)"
    $schemaSql += ""
    
    foreach ($tableRow in $userTables) {
        $tableName = $tableRow.TABLE_NAME
        
        # Get columns
        $columnsSchema = $conn.GetSchema("Columns", @( $null, $null, $tableName, $null ))
        
        $colDefs = @()
        foreach ($colRow in $columnsSchema) {
            $colName = $colRow.COLUMN_NAME
            $dataType = $colRow.DATA_TYPE
            $isNullable = $colRow.IS_NULLABLE -eq 'YES'
            $colSize = $colRow.CHARACTER_MAXIMUM_LENGTH
            $precision = $colRow.NUMERIC_PRECISION
            $scale = $colRow.NUMERIC_SCALE
            
            # Map Access types to PostgreSQL
            $pgType = switch ($dataType) {
                3   { "INTEGER" }                    # adInteger
                2   { "SMALLINT" }                   # adSmallInt
                20  { "BIGINT" }                     # adBigInt
                4   { "REAL" }                       # adSingle
                5   { "DOUBLE PRECISION" }           # adDouble
                6   { "NUMERIC($precision,$scale)" } # adCurrency
                131 { "NUMERIC($precision,$scale)" } # adNumeric
                7   { "DATE" }                       # adDate
                135 { "TIMESTAMP" }                  # adDBTimeStamp
                11  { "BOOLEAN" }                    # adBoolean
                72  { "TEXT" }                       # adVarWChar (Unicode text)
                202 { "VARCHAR($colSize)" }          # adVarWChar
                203 { "TEXT" }                       # adLongVarWChar (Memo)
                default { "TEXT" }
            }
            
            $nullable = if ($isNullable) { "" } else { " NOT NULL" }
            $colDefs += "    $colName $pgType$nullable"
        }
        
        $schemaSql += "CREATE TABLE $tableName ("
        $schemaSql += ($colDefs -join ",\n")
        $schemaSql += "\n);"
        $schemaSql += ""
    }
    
    $schemaSql | Out-File "$OutputDir\schema_postgres.sql" -Encoding UTF8
    
    # Create README
    @"
# Database Extraction Summary

**Source:** $MdbPath
**Password:** <PROTECTED_PASSWORD>
**Extracted:** $(Get-Date)
**Tables found:** $($userTables.Count)

## Files
- \`tables.txt\` - List of all table names
- \`row_counts.csv\` - Row count per table
- \`schema_postgres.sql\` - PostgreSQL CREATE TABLE statements
- \`<table>.csv\` - Data for each table

## Next Steps
1. Review \`schema_postgres.sql\` for data types
2. Clean up: Remove Jet/Access specific types
3. Add indexes, foreign keys, constraints
4. Create migration script for target database
"@ | Out-File "$OutputDir\README.md" -Encoding UTF8
    
    $conn.Close()
    
    Write-Host "`n==========================================" -ForegroundColor Green
    Write-Host "Extraction complete!" -ForegroundColor Green
    Write-Host "Output: $OutputDir\" -ForegroundColor Green
    Write-Host "==========================================" -ForegroundColor Green
    
    Get-ChildItem $OutputDir | Format-Table Name, Length, LastWriteTime
    
} catch {
    Write-Error "Failed to connect: $_"
    Write-Host "`nTroubleshooting:" -ForegroundColor Yellow
    Write-Host "1. Install 32-bit Access Database Engine:"
    Write-Host "   https://www.microsoft.com/en-us/download/details.aspx?id=54920"
    Write-Host "2. Run this script in 32-bit PowerShell:"
    Write-Host "   C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
    Write-Host "3. Verify password is correct if database is password-protected"
    exit 1
}