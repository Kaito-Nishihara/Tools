param(
    [string]$UpdateDir = ".\Update",
    [string]$OutFile = ".\Update_All.sql",
    [switch]$AddGo,
    [switch]$SortByName
)

if (-not (Test-Path -LiteralPath $UpdateDir)) {
    throw "UpdateDir が存在しません: $UpdateDir"
}

# UpdateDirは存在するので Resolve-Path OK
$updateFull = (Resolve-Path -LiteralPath $UpdateDir).Path

# OutFileは「存在しなくても」フルパス化できる GetFullPath を使う
$outFull = [System.IO.Path]::GetFullPath($OutFile)
$outDir = Split-Path -Parent $outFull
if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

# 対象SQL収集（出力ファイル自身は除外）
$files = Get-ChildItem -LiteralPath $updateFull -Recurse -File -Filter *.sql |
Where-Object { [System.IO.Path]::GetFullPath($_.FullName) -ne $outFull } |
Sort-Object (@{ Expression = { if ($SortByName) { $_.Name } else { $_.FullName } } })

# UTF-8(BOM付き) で出力（SSMS/SQLCMD向けに無難）
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
$sw = New-Object System.IO.StreamWriter($outFull, $false, $utf8Bom)

try {
    $sw.WriteLine("-- Auto-generated: merged SQL files from $updateFull")
    $sw.WriteLine("-- GeneratedAt: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    $sw.WriteLine()

    foreach ($f in $files) {
        $sw.WriteLine("-- ===== BEGIN: $($f.FullName) =====")

        $text = Get-Content -LiteralPath $f.FullName -Raw

        # 先頭BOM混入対策
        if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
            $text = $text.Substring(1)
        }

        $sw.Write($text.TrimEnd())
        $sw.WriteLine()
        $sw.WriteLine("-- ===== END: $($f.FullName) =====")
        $sw.WriteLine()

        if ($AddGo) {
            $sw.WriteLine("GO")
            $sw.WriteLine()
        }
    }
}
finally {
    $sw.Dispose()
}

Write-Host "Merged: $($files.Count) files -> $outFull"
