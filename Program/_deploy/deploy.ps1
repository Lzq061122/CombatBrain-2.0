param(
    [string]$GameDir = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord',
    [string]$ModuleDir = 'CombatBrain2'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'CombatBrain2.csproj'
$stampFile = Join-Path $root 'src\Host\BuildIdentity.g.cs'
$outDll = Join-Path $root 'bin\Debug\CombatBrain2.dll'
$destDir = Join-Path (Join-Path $GameDir 'Modules') $ModuleDir
$destBin = Join-Path $destDir 'bin\Win64_Shipping_Client'

Write-Host "=== CombatBrain2 deploy ==="

# 1. 有活进程时拒绝部署（忽略已退出的僵尸进程）。
$alive = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
if ($alive.Count -gt 0) {
    Write-Host "REFUSED: Bannerlord is running (pid $(($alive | ForEach-Object { $_.Id }) -join ','))"
    exit 2
}

# 2. 生成唯一 build_id 并写入 stamp 源文件。
$stamp = 'b' + (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + ([guid]::NewGuid().ToString('N').Substring(0, 8))
$stampSrc = @"
namespace CombatBrain2.Host
{
    internal static class BuildIdentity
    {
        internal const string BuildId = "$stamp";
    }
}
"@
[System.IO.File]::WriteAllText($stampFile, $stampSrc, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "build_id: $stamp"

# 3. 清空 obj/bin 强制真重编。
Remove-Item -Recurse -Force (Join-Path $root 'obj'), (Join-Path $root 'bin') -ErrorAction SilentlyContinue

# 4. 构建。
& dotnet build $proj -c Debug --nologo -v:m
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }
if (-not (Test-Path $outDll)) { Write-Host "BUILD OUTPUT MISSING: $outDll"; exit 1 }

# 5. 在编译产物里按两种 UTF-16 字节对齐搜索 build_id，确认 stamp 真进了 DLL。
$bytes = [System.IO.File]::ReadAllBytes($outDll)
$ascii = [System.Text.Encoding]::ASCII.GetBytes($stamp)
$utf16 = [System.Text.Encoding]::Unicode.GetBytes($stamp)
function Find-Bytes($hay, $needle) {
    if ($needle.Length -eq 0 -or $hay.Length -lt $needle.Length) { return $false }
    for ($i = 0; $i -le ($hay.Length - $needle.Length); $i++) {
        $ok = $true
        for ($j = 0; $j -lt $needle.Length; $j++) {
            if ($hay[$i + $j] -ne $needle[$j]) { $ok = $false; break }
        }
        if ($ok) { return $true }
    }
    return $false
}
$found = (Find-Bytes $bytes $ascii) -or (Find-Bytes $bytes $utf16)
if (-not $found) {
    Write-Host "STAMP NOT FOUND IN DLL - build_id did not reach the artifact"
    exit 1
}
Write-Host "stamp verified in dll"

# 6. 部署目标必须干净或属于本模块，不得覆盖来源不明的文件。
if (Test-Path $destDir) {
    $existing = @(Get-ChildItem -File $destDir -Recurse)
    $allowed = @('CombatBrain2.dll', 'SubModule.xml')
    $foreign = @($existing | Where-Object { $_.Name -notin $allowed })
    if ($foreign.Count -gt 0) {
        Write-Host "REFUSED: target contains files not produced by this module:"
        $foreign | ForEach-Object { Write-Host "  $($_.FullName)" }
        exit 3
    }
}
New-Item -ItemType Directory -Force -Path $destBin | Out-Null

# 7. 部署 DLL + SubModule.xml，并做源/部署 SHA256 比对。
# DLL 必须放 bin\Win64_Shipping_Client\；放模块根目录游戏不会加载 SubModule。
$srcHash = (Get-FileHash $outDll -Algorithm SHA256).Hash
Copy-Item $outDll (Join-Path $destBin 'CombatBrain2.dll') -Force
Copy-Item (Join-Path $root '_deploy\SubModule.xml') (Join-Path $destDir 'SubModule.xml') -Force

$dstDll = Join-Path $destBin 'CombatBrain2.dll'
$dstHash = (Get-FileHash $dstDll -Algorithm SHA256).Hash
if ($srcHash -ne $dstHash) {
    Write-Host "DEPLOY HASH MISMATCH: src=$srcHash dst=$dstHash"
    exit 1
}

Write-Host "source_sha256:   $srcHash"
Write-Host "deployed_sha256: $dstHash"
Write-Host "deployed_to:     $dstDll"
Write-Host "size:            $((Get-Item $dstDll).Length)"
Write-Host "DEPLOY OK"
exit 0
