# Deploy Claude CLI Runner as Windows Service
# Requires Administrator privileges

param(
    [Parameter(Mandatory=$true)]
    [string]$ServiceName = "ClaudeCLIRunner",
    
    [Parameter(Mandatory=$true)]
    [string]$InstallPath,
    
    [string]$DisplayName = "Claude CLI Runner Service",
    [string]$Description = "Processes Azure DevOps work items tagged with @Claude using Claude Code CLI",
    [string]$StartupType = "Automatic",
    [string]$ServiceAccount = "LocalSystem"
)

# Check if running as administrator
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

Write-Host "Claude CLI Runner Service Deployment" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green

# Build the project
Write-Host "`nBuilding project..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained -o $InstallPath

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}

# Copy configuration file
$configSource = Join-Path $PSScriptRoot "..\appsettings.json"
$configDest = Join-Path $InstallPath "appsettings.json"
Copy-Item $configSource $configDest -Force

Write-Host "Build completed successfully!" -ForegroundColor Green

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "`nService already exists. Stopping and removing..." -ForegroundColor Yellow
    
    if ($existingService.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    
    sc.exe delete $ServiceName
    Start-Sleep -Seconds 2
}

# Create the service
Write-Host "`nCreating Windows Service..." -ForegroundColor Yellow

$exePath = Join-Path $InstallPath "ClaudeCLIRunner.exe"

$params = @{
    Name = $ServiceName
    BinaryPathName = $exePath
    DisplayName = $DisplayName
    Description = $Description
    StartupType = $StartupType
}

New-Service @params

# Configure service recovery options
Write-Host "Configuring service recovery options..." -ForegroundColor Yellow
sc.exe failure $ServiceName reset=86400 actions=restart/60000/restart/60000/restart/60000

# Set service account if specified
if ($ServiceAccount -ne "LocalSystem") {
    Write-Host "Setting service account to: $ServiceAccount" -ForegroundColor Yellow
    $credential = Get-Credential -Message "Enter credentials for service account" -UserName $ServiceAccount
    
    $service = Get-WmiObject -Class Win32_Service -Filter "Name='$ServiceName'"
    $service.Change($null, $null, $null, $null, $null, $null, $credential.UserName, $credential.GetNetworkCredential().Password)
}

Write-Host "`nService installation completed!" -ForegroundColor Green
Write-Host "`nService Details:" -ForegroundColor Cyan
Write-Host "  Name: $ServiceName"
Write-Host "  Display Name: $DisplayName"
Write-Host "  Install Path: $InstallPath"
Write-Host "  Executable: $exePath"
Write-Host "  Startup Type: $StartupType"

Write-Host "`nTo start the service, run:" -ForegroundColor Yellow
Write-Host "  Start-Service -Name $ServiceName" -ForegroundColor White

Write-Host "`nTo view service logs, run:" -ForegroundColor Yellow
Write-Host "  Get-EventLog -LogName Application -Source $ServiceName -Newest 20" -ForegroundColor White

Write-Host "`nTo uninstall the service, run:" -ForegroundColor Yellow
Write-Host "  Stop-Service -Name $ServiceName; sc.exe delete $ServiceName" -ForegroundColor White 