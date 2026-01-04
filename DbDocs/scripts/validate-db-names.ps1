<#
.SYNOPSIS
  Validate DB object names in EF Core migration files.

.DESCRIPTION
  Scans C# migration files under a migrations directory and validates
  naming conventions for tables, primary keys (PK_), foreign keys (FK_),
  indexes (IX_), unique constraints (UQ_), etc. Exits with non-zero
  code if violations are found so CI can fail the job.

.PARAMETER MigrationsDir
  Path to the directory that contains EF migration .cs files. Defaults to 'NK.Entities/db/Migrations'.

.EXAMPLE
  pwsh .\validate-db-names.ps1 -MigrationsDir "NK.Entities/db/Migrations"
#>
param(
    [string]$MigrationsDir = "NK.Entities/db/Migrations"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- patterns ---
$patterns = [ordered]@{
    Table = '^[A-Z][A-Za-z0-9]+$'
    PK    = '^PK_[A-Z][A-Za-z0-9]+$'
    FK    = '^FK_[A-Z][A-Za-z0-9]+_[A-Z][A-Za-z0-9]+_[A-Za-z0-9]+$'
    IX    = '^IX_[A-Z][A-Za-z0-9]+_[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*$'
    UQ    = '^UQ_[A-Z][A-Za-z0-9]+_[A-Za-z0-9]+$'
}

function Test-Regex([string]$value, [string]$pattern) {
    if ([string]::IsNullOrEmpty($value)) { return $true } # skip empty
    return [bool]([regex]::IsMatch($value, $pattern))
}

function Fail([string]$msg) {
    Write-Host "ERROR: $msg" -ForegroundColor Red
    $script:HasError = $true
}

Write-Host "Scanning migrations in: $MigrationsDir"
if (-not (Test-Path $MigrationsDir)) {
    Write-Host "Directory not found: $MigrationsDir" -ForegroundColor Yellow
    Exit 1
}

$files = Get-ChildItem -Path $MigrationsDir -Filter *.cs -File -Recurse
if ($files.Count -eq 0) {
    Write-Host "No migration files found in $MigrationsDir" -ForegroundColor Yellow
    Exit 0
}

$script:HasError = $false

foreach ($file in $files) {
    $lines = Get-Content -Path $file.FullName -Raw -ErrorAction Stop

    # 1) Table names: CreateTable( name: "{Table}",
    $createTableRegex = [regex]'CreateTable\s*\(\s*name\s*:\s*"(?<name>[^"]+)"'
    foreach ($m in $createTableRegex.Matches($lines)) {
        $name = $m.Groups['name'].Value
        if (-not (Test-Regex $name $patterns.Table)) {
            Fail("Invalid table name '$name' in $($file.Name). Expected pattern: $($patterns.Table)")
        }
    }

    # 2) Primary keys: table.PrimaryKey("PK_..."
    $pkRegex = [regex]'PrimaryKey\(\s*"(?<name>[^"\)]+)"'
    foreach ($m in $pkRegex.Matches($lines)) {
        $name = $m.Groups['name'].Value
        if (-not (Test-Regex $name $patterns.PK)) {
            Fail("Invalid PK name '$name' in $($file.Name). Expected pattern: $($patterns.PK)")
        }
    }

    # 3) Foreign keys: table.ForeignKey( name: "FK_..." or migrationBuilder.AddForeignKey(..., name: "FK_...")
    $fkRegex = [regex]'ForeignKey\s*\(\s*(?:name\s*:\s*"(?<name1>[^"]+)"|\s*"(?<name2>FK_[^"\)]+)" )'
    # fallback simpler pattern
    $fkRegex2 = [regex]'name\s*:\s*"(?<name>FK_[^"]+)"'
    foreach ($m in $fkRegex2.Matches($lines)) {
        $name = $m.Groups['name'].Value
        if (-not (Test-Regex $name $patterns.FK)) {
            Fail("Invalid FK name '$name' in $($file.Name). Expected pattern: $($patterns.FK)")
        }
    }

    # 4) Indexes: migrationBuilder.CreateIndex( name: "IX_..."
    $idxRegex = [regex]'CreateIndex\s*\(\s*name\s*:\s*"(?<name>[^"]+)"'
    foreach ($m in $idxRegex.Matches($lines)) {
        $name = $m.Groups['name'].Value
        if (-not (Test-Regex $name $patterns.IX)) {
            Fail("Invalid index name '$name' in $($file.Name). Expected pattern: $($patterns.IX)")
        }
    }

    # 5) Unique constraints (rare in migrations): name: "UQ_..."
    $uqRegex = [regex]'name\s*:\s*"(?<name>UQ_[^"]+)"'
    foreach ($m in $uqRegex.Matches($lines)) {
        $name = $m.Groups['name'].Value
        if (-not (Test-Regex $name $patterns.UQ)) {
            Fail("Invalid unique constraint name '$name' in $($file.Name). Expected pattern: $($patterns.UQ)")
        }
    }
}

if ($script:HasError) {
    Write-Host "One or more naming violations were found." -ForegroundColor Red
    Exit 1
}
else {
    Write-Host "OK: All checked names comply with the naming rules." -ForegroundColor Green
    Exit 0
}
