<#
.SYNOPSIS
  Creates database [Claim] on LocalDB (if missing) and runs api/db/NNN_*.sql in order.
.EXAMPLE
  ./Initialize-Database.ps1
  ./Initialize-Database.ps1 -Server '(localdb)\MSSQLLocalDB' -Database Claim
#>
[CmdletBinding()]
param(
    [string]$Server = '(localdb)\MSSQLLocalDB',
    [string]$Database = 'Claim'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Data

function Invoke-Sql([string]$Db, [string]$Sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection(
        "Server=$Server;Database=$Db;Trusted_Connection=True;TrustServerCertificate=True")
    $conn.Open()
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        [void]$cmd.ExecuteNonQuery()
    }
    finally { $conn.Dispose() }
}

# LocalDB instance may be stopped; sqllocaldb start is harmless if already running.
if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
    $instance = ($Server -replace '^\(localdb\)\\', '')
    sqllocaldb start $instance | Out-Null
}

Write-Host "Ensuring database [$Database] exists..."
$escaped = $Database.Replace(']', ']]')
Invoke-Sql 'master' "IF DB_ID(N'$($Database.Replace("'", "''"))') IS NULL CREATE DATABASE [$escaped];"

$scripts = Get-ChildItem -Path $PSScriptRoot -Filter '*.sql' |
    Where-Object { $_.Name -match '^\d{3}_.*\.sql$' } | Sort-Object Name
if (-not $scripts) { throw "No NNN_*.sql scripts found in $PSScriptRoot" }
foreach ($script in $scripts) {
    Write-Host "Running $($script.Name)"
    $text = Get-Content -Path $script.FullName -Raw -Encoding UTF8
    # Split on GO batch separators (a line containing only GO).
    $batches = [regex]::Split($text, '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() }
    foreach ($batch in $batches) { Invoke-Sql $Database $batch }
}

Write-Host "Done. Database [$Database] is ready on $Server."
