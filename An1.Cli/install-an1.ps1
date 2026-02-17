# install-an1.ps1
# AN1 CLI を Azure Artifacts から global tool としてインストールし、PATH を永久に通す

$ErrorActionPreference = "Stop"

$feedUrl = "https://pkgs.dev.azure.com/nishihara-dev-studio/_packaging/au2485731/nuget/v3/index.json"
$packageId = "An1.Cli"
$toolsDir = Join-Path $env:USERPROFILE ".dotnet\tools"

Write-Host "=== AN1 CLI インストール開始 ==="
Write-Host "Feed   : $feedUrl"
Write-Host "Package: $packageId"
Write-Host "Tools  : $toolsDir"
Write-Host ""

# 1) インストール（既に入っている場合は update に切り替える）
Write-Host "[1/4] dotnet tool install/update..."
try {
    dotnet tool install -g $packageId --add-source $feedUrl | Out-Host
}
catch {
    # 既にインストール済みのときは update を試す
    Write-Host "  install に失敗（既に入っている可能性）: update を試します..."
    dotnet tool update -g $packageId --add-source $feedUrl | Out-Host
}

# 2) PATH 永久追加（ユーザー環境変数）
Write-Host ""
Write-Host "[2/4] ユーザーPATHへ追加..."
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ([string]::IsNullOrWhiteSpace($userPath)) { $userPath = "" }

$already = $userPath.Split(';') -contains $toolsDir
if (-not $already) {
    $newUserPath = ($userPath.TrimEnd(';') + ';' + $toolsDir).Trim(';')
    [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
    Write-Host "  OK: ユーザーPATHに追加しました -> $toolsDir"
}
else {
    Write-Host "  OK: 既にユーザーPATHに入っています -> $toolsDir"
}

# 3) この PowerShell セッションでも即時反映（新しいターミナルを開くまでのつなぎ）
Write-Host ""
Write-Host "[3/4] 現在セッションの PATH も更新..."
if (-not ($env:Path.Split(';') -contains $toolsDir)) {
    $env:Path = ($env:Path.TrimEnd(';') + ';' + $toolsDir)
    Write-Host "  OK: 現在セッションにも反映しました"
}
else {
    Write-Host "  OK: 現在セッションには既に反映済みです"
}

# 4) 動作確認
Write-Host ""
Write-Host "[4/4] 動作確認..."
Write-Host "where an1:"
where.exe an1 2>$null | Out-Host

Write-Host ""
Write-Host "an1 --version:"
try {
    an1 --version | Out-Host
    Write-Host ""
    Write-Host "=== 完了: an1 が使用可能です ==="
    Write-Host "※ 他のターミナルでも使うには、一度ターミナルを閉じて開き直してください。"
}
catch {
    Write-Host ""
    Write-Host "WARNING: このセッションでは an1 が見つかりませんでした。"
    Write-Host "  - ターミナルを閉じて開き直してください（ユーザーPATH反映のため）"
    Write-Host "  - または次で直接実行できます:"
    Write-Host "    & `"$toolsDir\an1.exe`" --version"
    exit 2
}
