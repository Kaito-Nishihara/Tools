param(
  [Parameter(Mandatory = $true)][string]$ConnectionString,
  [Parameter(Mandatory = $true)][string]$OutDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Query([string]$sql) {
  Add-Type -AssemblyName System.Data
  $conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
  $cmd = $conn.CreateCommand()
  $cmd.CommandText = $sql
  $cmd.CommandTimeout = 120

  $dt = New-Object System.Data.DataTable
  $conn.Open()
  try {
    $r = $cmd.ExecuteReader()
    $dt.Load($r)
    # DataTable を返す（Get-Rows 側で配列化する）
    Write-Output -NoEnumerate $dt
  }
  finally {
    $conn.Close()
  }
}

# Get-Rows: 何が来ても「配列」を返す（DataTable / DataRowCollection / DataRow / 単一オブジェクト）
function Get-Rows($obj) {
  if ($null -eq $obj) { return @() }

  if ($obj -is [System.Data.DataTable]) {
    # DataTable.Rows は IEnumerable を実装しているので列挙して配列化する
    return @($obj.Rows | ForEach-Object { $_ })
  }

  if ($obj -is [System.Data.DataRowCollection]) {
    return @($obj | ForEach-Object { $_ })
  }

  if ($obj -is [System.Data.DataRow]) {
    return @($obj)
  }

  # その他の IEnumerable/単一オブジェクトに対しても配列化して返す
  if ($obj -is [System.Collections.IEnumerable] -and -not ($obj -is [string])) {
    return @($obj | ForEach-Object { $_ })
  }

  return @($obj)
}

function To-StrOrEmpty($v) {
  if ($v -ne $null) { return [string]$v }
  return ""
}

function MdSafe($s) {
  if ([string]::IsNullOrEmpty($s)) { return "" }
  return ($s -replace '\|', '&#124;')
}

function To-Br($s) {
  if ([string]::IsNullOrEmpty($s)) { return "" }
  return (MdSafe $s) -replace "\r?\n", "<br>"
}

function Format-Type($r) {
  $type = $r.type_name
  if ($type -in @("nvarchar","nchar","varchar","char","varbinary","binary")) {
    $len = [int]$r.max_length
    if ($type -like "n*") { $len = [int]($len / 2) }
    if ($len -eq -1) { return "$type(max)" }
    return "$type($len)"
  }
  if ($type -in @("decimal","numeric")) { return "$type($($r.precision),$($r.scale))" }
  if ($type -in @("datetime2","time")) { return "$type($($r.scale))" }
  return $type
}

function Safe-Join($items) {
  $arr = @($items) | Where-Object { $_ -ne $null -and $_.ToString() -ne "" } | ForEach-Object { $_.ToString() }
  if ((@($arr) | Measure-Object).Count -eq 0) { return "" }
  return [string]::Join(", ", $arr)
}

# ---- DBML helpers ----
function Dbml-QuoteTable([string]$schema, [string]$table) {
  return '"' + $schema + '.' + $table + '"'
}
function Dbml-QuoteColumn([string]$col) {
  if ($col -match '^[A-Za-z_][A-Za-z0-9_]*$') { return $col }
  return '"' + ($col -replace '"','\"') + '"'
}

# ---- output dirs ----
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $OutDir "tables") | Out-Null

# ---- SQL ----
$sqlTables = @"
SELECT s.name AS schema_name, t.name AS table_name, CAST(ep.value AS nvarchar(max)) AS description
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
LEFT JOIN sys.extended_properties ep
  ON ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
ORDER BY s.name, t.name;
"@

$sqlColumns = @"
;WITH pk_cols AS (
  SELECT ic.object_id, ic.column_id
  FROM sys.indexes i
  JOIN sys.index_columns ic
    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  WHERE i.is_primary_key = 1
)
SELECT
  s.name AS schema_name,
  t.name AS table_name,
  c.column_id,
  c.name AS column_name,
  ty.name AS type_name,
  c.max_length,
  c.precision,
  c.scale,
  c.is_nullable,
  dc.definition AS default_definition,
  ic.seed_value,
  ic.increment_value,
  cc.definition AS computed_definition,
  CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS is_pk,
  CAST(ep.value AS nvarchar(max)) AS description
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints dc
  ON dc.parent_object_id = t.object_id AND dc.parent_column_id = c.column_id
LEFT JOIN sys.identity_columns ic
  ON ic.object_id = t.object_id AND ic.column_id = c.column_id
LEFT JOIN sys.computed_columns cc
  ON cc.object_id = t.object_id AND cc.column_id = c.column_id
LEFT JOIN pk_cols pk
  ON pk.object_id = t.object_id AND pk.column_id = c.column_id
LEFT JOIN sys.extended_properties ep
  ON ep.major_id = t.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
ORDER BY s.name, t.name, c.column_id;
"@

$sqlIndexes = @"
SELECT
  s.name AS schema_name,
  t.name AS table_name,
  i.name AS index_name,
  i.type_desc,
  i.is_unique,
  i.is_primary_key,
  i.is_unique_constraint,
  ic.key_ordinal,
  c.name AS column_name,
  ic.is_descending_key,
  ic.is_included_column
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.indexes i ON i.object_id = t.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = ic.column_id
WHERE i.type_desc <> 'HEAP'
ORDER BY s.name, t.name, i.name, ic.is_included_column, ic.key_ordinal;
"@

$sqlFks = @"
SELECT
  ps.name AS parent_schema,
  pt.name AS parent_table,
  pc.name AS parent_column,
  rs.name AS ref_schema,
  rt.name AS ref_table,
  rc.name AS ref_column,
  fk.name AS fk_name,
  fk.delete_referential_action_desc AS on_delete,
  fk.update_referential_action_desc AS on_update
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables pt ON pt.object_id = fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id = pt.schema_id
JOIN sys.columns pc ON pc.object_id = pt.object_id AND pc.column_id = fkc.parent_column_id
JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fkc.referenced_column_id
ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id;
"@

# ---- Query ----
$tables = Invoke-Query $sqlTables
$cols   = Invoke-Query $sqlColumns
$idxs   = Invoke-Query $sqlIndexes
$fks    = Invoke-Query $sqlFks

$tablesRows = Get-Rows $tables
if ((@($tablesRows) | Measure-Object).Count -eq 0) {
  throw "No tables found. Check connection string / permissions."
}

# ---- index.md ----
$index = New-Object System.Text.StringBuilder
$null = $index.AppendLine("# DB Dictionary")
$null = $index.AppendLine("")
$null = $index.AppendLine("## Tables")
$null = $index.AppendLine("| Schema | Table | Description |")
$null = $index.AppendLine("|---|---|---|")

foreach ($t in $tablesRows) {
  $schema = $t.schema_name
  $table  = $t.table_name
  $desc   = To-Br (To-StrOrEmpty $t.description)
  $link   = "tables/$schema.$table.md"
  $null = $index.AppendLine("| $schema | [$table]($link) | $desc |")
}

[IO.File]::WriteAllText(
  (Join-Path $OutDir "index.md"),
  $index.ToString(),
  ([System.Text.UTF8Encoding]::new($false))
)

# ---- table docs ----
foreach ($t in $tablesRows) {
  $schema = $t.schema_name
  $table  = $t.table_name
  $desc   = To-StrOrEmpty $t.description

  $md = New-Object System.Text.StringBuilder
  $null = $md.AppendLine("# $schema.$table")
  if (-not [string]::IsNullOrEmpty($desc)) {
    $null = $md.AppendLine("**Description:** $(To-Br $desc)")
  }

  $null = $md.AppendLine("")
  $null = $md.AppendLine("## Columns")
  $null = $md.AppendLine("| No | Column | Type | NULL | Default | PK | Description |")
  $null = $md.AppendLine("|---:|---|---|---:|---|---:|---|")

  $colRows = Get-Rows $cols | Where-Object { $_.schema_name -eq $schema -and $_.table_name -eq $table } | Sort-Object column_id
  foreach ($r in $colRows) {
    $no = $r.column_id
    $col = $r.column_name
    $type = Format-Type $r
    $nullable = if ([int]$r.is_nullable -eq 1) { "Y" } else { "N" }
    $def = To-Br (To-StrOrEmpty $r.default_definition)
    $pk  = if ([int]$r.is_pk -eq 1) { "Y" } else { "" }
    $cdesc = To-Br (To-StrOrEmpty $r.description)

    $null = $md.AppendLine("| $no | $col | $type | $nullable | $def | $pk | $cdesc |")
  }

  # Indexes
  $null = $md.AppendLine("")
  $null = $md.AppendLine("## Indexes")
  $null = $md.AppendLine("| Name | Type | Unique | PK | Columns | Include |")
  $null = $md.AppendLine("|---|---|---:|---:|---|---|")

  $idxRows = Get-Rows $idxs | Where-Object { $_.schema_name -eq $schema -and $_.table_name -eq $table }
  if ((@($idxRows) | Measure-Object).Count -gt 0) {
    $byIndex = $idxRows | Group-Object index_name
    foreach ($g in $byIndex) {
      $any = $g.Group[0]
      $name = $any.index_name
      $typeDesc = $any.type_desc
      $uniq = if ([bool]$any.is_unique) { "Y" } else { "" }
      $isPk = if ([bool]$any.is_primary_key) { "Y" } else { "" }

      $keys = $g.Group | Where-Object { -not $_.is_included_column } | Sort-Object key_ordinal | ForEach-Object { $d = if ($_.is_descending_key) { " DESC" } else { "" }; "$($_.column_name)$d" }
      $incs = $g.Group | Where-Object { $_.is_included_column } | ForEach-Object { $_.column_name }

      $null = $md.AppendLine("| $name | $typeDesc | $uniq | $isPk | $(Safe-Join $keys) | $(Safe-Join $incs) |")
    }
  }
  else {
    $null = $md.AppendLine("| (none) |  |  |  |  |  |")
  }

  # Foreign Keys
  $null = $md.AppendLine("")
  $null = $md.AppendLine("## Foreign Keys")
  $null = $md.AppendLine("| FK Name | From | To | OnDelete | OnUpdate |")
  $null = $md.AppendLine("|---|---|---|---|---|")

  $fkRows = Get-Rows $fks | Where-Object { $_.parent_schema -eq $schema -and $_.parent_table -eq $table }
  if ((@($fkRows) | Measure-Object).Count -gt 0) {
    foreach ($fk in $fkRows) {
      $fkName = MdSafe (To-StrOrEmpty $fk.fk_name)
      $from = "$($fk.parent_schema).$($fk.parent_table).$($fk.parent_column)"
      $to   = "$($fk.ref_schema).$($fk.ref_table).$($fk.ref_column)"
      $null = $md.AppendLine("| $fkName | $from | $to | $($fk.on_delete) | $($fk.on_update) |")
    }
  }
  else {
    $null = $md.AppendLine("| (none) |  |  |  |  |")
  }

  $outPath = Join-Path $OutDir ("tables/{0}.{1}.md" -f $schema, $table)
  [IO.File]::WriteAllText(
    $outPath,
    $md.ToString(),
    ([System.Text.UTF8Encoding]::new($false))
  )
}

# ---- DBML (dbdiagram) ----
$dbml = New-Object System.Text.StringBuilder

foreach ($t in $tablesRows) {
  $schema = $t.schema_name
  $table  = $t.table_name
  $tq = Dbml-QuoteTable $schema $table

  $null = $dbml.AppendLine("Table $tq {")
  $colRows = Get-Rows $cols | Where-Object { $_.schema_name -eq $schema -and $_.table_name -eq $table } | Sort-Object column_id

  $pkCols = @($colRows | Where-Object { [int]$_.is_pk -eq 1 } | ForEach-Object { $_.column_name })

  foreach ($c in $colRows) {
    $colName = Dbml-QuoteColumn (To-StrOrEmpty $c.column_name)
    $colType = Format-Type $c

    $attrs = @()
    if ($pkCols.Count -eq 1 -and [int]$c.is_pk -eq 1) { $attrs += "pk" }
    if ([int]$c.is_nullable -eq 0) { $attrs += "not null" }
    if ($c.seed_value -ne $null) { $attrs += "increment" }

    $attrText = ""
    if ($attrs.Count -gt 0) { $attrText = " [" + (Safe-Join $attrs) + "]" }

    $null = $dbml.AppendLine("  $colName $colType$attrText")
  }

  if ($pkCols.Count -gt 1) {
    $null = $dbml.AppendLine("")
    $null = $dbml.AppendLine("  Indexes {")
    $colsJoined = ($pkCols | ForEach-Object { Dbml-QuoteColumn $_ }) -join ", "
    $null = $dbml.AppendLine("    ($colsJoined) [pk]")
    $null = $dbml.AppendLine("  }")
  }

  $null = $dbml.AppendLine("}")
  $null = $dbml.AppendLine("")
}

$fkRowsAll = Get-Rows $fks
foreach ($fk in $fkRowsAll) {
  $parentTable = Dbml-QuoteTable $fk.parent_schema $fk.parent_table
  $refTable    = Dbml-QuoteTable $fk.ref_schema    $fk.ref_table
  $parentCol   = Dbml-QuoteColumn (To-StrOrEmpty $fk.parent_column)
  $refCol      = Dbml-QuoteColumn (To-StrOrEmpty $fk.ref_column)

  $null = $dbml.AppendLine("Ref: $refTable.$refCol < $parentTable.$parentCol")
}

[IO.File]::WriteAllText(
  (Join-Path $OutDir "schema.dbml"),
  $dbml.ToString(),
  ([System.Text.UTF8Encoding]::new($false))
)

Write-Host "✅ Generated: $OutDir"
Write-Host "   - index.md"
Write-Host "   - tables/*.md"
Write-Host "   - schema.dbml (for dbdiagram)"