param(
    [string]$GameDir = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord',
    [string]$ModuleDir = 'CombatBrain2',
    [string]$StampFile = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'CombatBrain2.csproj'
if ($StampFile) {
    $stampFile = $StampFile
} else {
    $stampFile = Join-Path $root 'src\Host\BuildIdentity.g.cs'
}
$outDll = Join-Path $root 'bin\Debug\CombatBrain2.dll'
$destDir = Join-Path (Join-Path $GameDir 'Modules') $ModuleDir
$destBin = Join-Path $destDir 'bin\Win64_Shipping_Client'

Write-Host "=== CombatBrain2 deploy ==="

# 1. Refuse to deploy while a live game process exists.
$alive = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
if ($alive.Count -gt 0) {
    Write-Host "REFUSED: Bannerlord is running (pid $(($alive | ForEach-Object { $_.Id }) -join ','))"
    exit 2
}

# 2. Generate a unique build_id into the stamp source file.
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

# 3. Clear obj/bin to force a real rebuild.
Remove-Item -Recurse -Force (Join-Path $root 'obj'), (Join-Path $root 'bin') -ErrorAction SilentlyContinue

# 4. Build.
& dotnet build $proj -c Debug --nologo -v:m
if ($LASTEXITCODE -ne 0) { Write-Host "BUILD FAILED"; exit 1 }
if (-not (Test-Path $outDll)) { Write-Host "BUILD OUTPUT MISSING: $outDll"; exit 1 }

# 5. Prove the stamp reached the artifact (ASCII or UTF-16LE scan).
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

# 6. The deploy target must be clean or belong to this module.
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

# 7. Deploy the DLL plus SubModule.xml and compare source/deployed SHA256.
# The DLL must live in bin\Win64_Shipping_Client\; the module root is not loaded.
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
