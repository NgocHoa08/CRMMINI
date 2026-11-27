param(
    [string]$PublishSource = "W:\CRM Mini\publish\api",
    [string]$TargetDir = "C:\inetpub\crmmini-api",
    [string]$SiteName = "CRMMiniAPI",
    [string]$AppPoolName = "CRMMiniApiPool",
    [int]$HttpPort = 5080,
    [string]$SqlServerInstance = ".\SQLEXPRESS",
    [string]$DatabaseName = "CRMMiniDb",
    [string]$EnvironmentName = "Staging",
    [string]$JwtSecret = ""
)

$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "Vui lòng chạy script này từ PowerShell hoặc Command Prompt với quyền Administrator."
}

$ancmPath = "C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if (-not (Test-Path $ancmPath)) {
    throw "Không tìm thấy AspNetCoreModuleV2 tại '$ancmPath'. Hãy cài .NET 10 Hosting Bundle trước."
}

$appCmd = Join-Path $env:windir "system32\inetsrv\appcmd.exe"
if (-not (Test-Path $appCmd)) {
    throw "Không tìm thấy IIS appcmd.exe tại '$appCmd'."
}

if (-not (Test-Path $PublishSource)) {
    throw "Không tìm thấy thư mục publish '$PublishSource'. Hãy chạy 'dotnet publish -c Release -o publish/api' trước."
}

if ([string]::IsNullOrWhiteSpace($JwtSecret)) {
    $bytes = New-Object byte[] 36
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $JwtSecret = [Convert]::ToBase64String($bytes)
}

Write-Host "[1/6] Copy gói publish vào $TargetDir..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $TargetDir "logs") | Out-Null

# Stop AppPool before overwriting DLLs if it already exists
$existingPools = & $appCmd list apppool /name:"$AppPoolName" 2>$null
if ($LASTEXITCODE -eq 0 -and $existingPools) {
    & $appCmd stop apppool /apppool.name:"$AppPoolName" | Out-Null
    Start-Sleep -Seconds 1
}

robocopy $PublishSource $TargetDir /MIR /XD logs | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy thất bại với mã lỗi $LASTEXITCODE."
}
New-Item -ItemType Directory -Force -Path (Join-Path $TargetDir "logs") | Out-Null

Write-Host "[2/6] Cấu hình IIS Application Pool '$AppPoolName' và Site '$SiteName' (port $HttpPort)..." -ForegroundColor Cyan
if (-not ($existingPools)) {
    & $appCmd add apppool /name:"$AppPoolName" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated"
} else {
    & $appCmd set apppool /apppool.name:"$AppPoolName" /managedRuntimeVersion:"" /managedPipelineMode:"Integrated"
}

$existingSite = & $appCmd list site /name:"$SiteName" 2>$null
if ($LASTEXITCODE -eq 0 -and $existingSite) {
    & $appCmd set site /site.name:"$SiteName" "/[path='/'].physicalPath:$TargetDir"
    & $appCmd set site /site.name:"$SiteName" "/[path='/'].applicationPool:$AppPoolName"
} else {
    & $appCmd add site /name:"$SiteName" "/bindings:http/*:${HttpPort}:" "/physicalPath:$TargetDir"
    & $appCmd set site /site.name:"$SiteName" "/[path='/'].applicationPool:$AppPoolName"
}

Write-Host "[3/6] Cấp quyền thư mục NTFS cho 'IIS AppPool\$AppPoolName'..." -ForegroundColor Cyan
icacls $TargetDir /grant "IIS AppPool\${AppPoolName}:(OI)(CI)RX" /T | Out-Null
icacls (Join-Path $TargetDir "logs") /grant "IIS AppPool\${AppPoolName}:(OI)(CI)M" /T | Out-Null

Write-Host "[4/6] Cập nhật biến môi trường IIS trong $TargetDir\web.config..." -ForegroundColor Cyan
$webConfigPath = Join-Path $TargetDir "web.config"
[xml]$xml = Get-Content $webConfigPath -Raw -Encoding UTF8
$aspNetCore = $xml.configuration.location.'system.webServer'.aspNetCore
if ($null -eq $aspNetCore) {
    throw "Không tìm thấy thẻ <aspNetCore> trong $webConfigPath."
}

$oldEnv = $aspNetCore.SelectSingleNode("environmentVariables")
if ($null -ne $oldEnv) {
    [void]$aspNetCore.RemoveChild($oldEnv)
}

$envVars = $xml.CreateElement("environmentVariables")
$vars = @{
    "ASPNETCORE_ENVIRONMENT"              = $EnvironmentName
    "ConnectionStrings__DefaultConnection" = "Server=$SqlServerInstance;Database=$DatabaseName;Trusted_Connection=True;TrustServerCertificate=True;"
    "JwtSettings__SecretKey"               = $JwtSecret
    "Swagger__Enabled"                     = "true"
    "Database__InitializeOnStartup"        = "true"
    "Database__AutoMigrateOnStartup"       = "false"
    "SeedData__Enabled"                    = "false"
}
foreach ($kv in $vars.GetEnumerator()) {
    $el = $xml.CreateElement("environmentVariable")
    $el.SetAttribute("name", $kv.Key)
    $el.SetAttribute("value", $kv.Value)
    [void]$envVars.AppendChild($el)
}
[void]$aspNetCore.AppendChild($envVars)
$xml.Save($webConfigPath)

Write-Host "[5/6] Cấp quyền SQL Server ($SqlServerInstance -> $DatabaseName) cho 'IIS APPPOOL\$AppPoolName'..." -ForegroundColor Cyan
$sqlCmdPath = Get-ChildItem "C:\Program Files\Microsoft SQL Server" -Filter "SQLCMD.EXE" -Recurse -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName
if (-not $sqlCmdPath) {
    throw "Không tìm thấy SQLCMD.EXE trong C:\Program Files\Microsoft SQL Server."
}

$sqlGrant = @"
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'IIS APPPOOL\$AppPoolName')
    CREATE LOGIN [IIS APPPOOL\$AppPoolName] FROM WINDOWS;
USE [$DatabaseName];
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'IIS APPPOOL\$AppPoolName')
    CREATE USER [IIS APPPOOL\$AppPoolName] FOR LOGIN [IIS APPPOOL\$AppPoolName];
ALTER ROLE [db_datareader] ADD MEMBER [IIS APPPOOL\$AppPoolName];
ALTER ROLE [db_datawriter] ADD MEMBER [IIS APPPOOL\$AppPoolName];
"@
& $sqlCmdPath -S $SqlServerInstance -E -b -Q $sqlGrant
if ($LASTEXITCODE -ne 0) {
    throw "Cấp quyền SQL Server thất bại."
}

Write-Host "[6/6] Khởi động lại IIS AppPool & Site và kiểm tra /health/ready..." -ForegroundColor Cyan
& $appCmd start apppool /apppool.name:"$AppPoolName" | Out-Null
& $appCmd start site /site.name:"$SiteName" | Out-Null

Start-Sleep -Seconds 2
$healthReady = Invoke-WebRequest -Uri "http://localhost:$HttpPort/health/ready" -UseBasicParsing -TimeoutSec 15
Write-Host "IIS Deploy thành công! http://localhost:$HttpPort/health/ready => $($healthReady.StatusCode) $($healthReady.Content)" -ForegroundColor Green
Write-Host "Swagger UI: http://localhost:$HttpPort/swagger" -ForegroundColor Green
