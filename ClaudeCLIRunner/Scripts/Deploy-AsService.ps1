# Deploy Claude CLI Runner as Windows Service
# Requires Administrator privileges
# SECURITY: This script handles sensitive operations and should be run with minimal privileges

#Requires -RunAsAdministrator

param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$ServiceName = "ClaudeCLIRunner",
    
    [Parameter(Mandatory=$true)]
    [ValidateScript({
        if (Test-Path $_ -IsValid) { $true }
        else { throw "Invalid path: $_" }
    })]
    [string]$InstallPath,
    
    [ValidateNotNullOrEmpty()]
    [string]$DisplayName = "Claude CLI Runner Service",
    
    [ValidateNotNullOrEmpty()]
    [string]$Description = "Processes Azure DevOps work items tagged with @Claude using Claude Code CLI",
    
    [ValidateSet("Automatic", "Manual", "Disabled")]
    [string]$StartupType = "Automatic",
    
    [ValidateSet("LocalSystem", "LocalService", "NetworkService")]
    [string]$ServiceAccount = "LocalSystem",
    
    [switch]$SkipBackup
)

# Security: Set strict error handling
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# Security: Validate script is running with appropriate privileges
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator for security reasons."
    exit 1
}

Write-Host "Claude CLI Runner Service Deployment" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green
Write-Host "SECURITY NOTE: This script will install a Windows service with system privileges." -ForegroundColor Yellow

# Security: Validate and sanitize inputs
$ServiceName = $ServiceName.Trim()
$InstallPath = $InstallPath.Trim()
$DisplayName = $DisplayName.Trim()
$Description = $Description.Trim()

# Security: Validate service name format
if ($ServiceName -notmatch '^[a-zA-Z0-9._-]+$') {
    Write-Error "Service name contains invalid characters. Only alphanumeric, dots, underscores, and hyphens are allowed."
    exit 1
}

# Security: Validate install path
if (-not (Split-Path $InstallPath -IsAbsolute)) {
    Write-Error "Install path must be an absolute path for security reasons."
    exit 1
}

# Security: Ensure install path is in a secure location
$secureLocations = @("C:\Program Files", "C:\Program Files (x86)", "C:\ProgramData")
$isSecureLocation = $false
foreach ($location in $secureLocations) {
    if ($InstallPath.StartsWith($location, [StringComparison]::OrdinalIgnoreCase)) {
        $isSecureLocation = $true
        break
    }
}

if (-not $isSecureLocation) {
    Write-Warning "Install path '$InstallPath' is not in a standard secure location. Consider using a location under Program Files."
    $confirm = Read-Host "Do you want to continue? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Installation cancelled."
        exit 0
    }
}

# Build the project
Write-Host "`nBuilding project..." -ForegroundColor Yellow
try {
    dotnet publish -c Release -r win-x64 --self-contained -o $InstallPath
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
}
catch {
    Write-Error "Build failed: $($_.Exception.Message)"
    exit 1
}

# Security: Backup existing installation if it exists
if (-not $SkipBackup -and (Test-Path $InstallPath)) {
    $backupPath = "$InstallPath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Write-Host "Creating backup at: $backupPath" -ForegroundColor Yellow
    Copy-Item $InstallPath $backupPath -Recurse -Force
}

# Copy configuration file with secure permissions
$configSource = Join-Path $PSScriptRoot "..\appsettings.json"
$configDest = Join-Path $InstallPath "appsettings.json"

if (Test-Path $configSource) {
    Copy-Item $configSource $configDest -Force
    
    # Security: Set restrictive permissions on config file
    $acl = Get-Acl $configDest
    $acl.SetAccessRuleProtection($true, $false)  # Remove inherited permissions
    $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "Allow")))
    $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("Administrators", "FullControl", "Allow")))
    Set-Acl $configDest $acl
    
    Write-Host "Configuration file permissions secured." -ForegroundColor Green
}

Write-Host "Build completed successfully!" -ForegroundColor Green

# Check if service exists
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "`nService already exists. Stopping and removing..." -ForegroundColor Yellow
    
    if ($existingService.Status -eq 'Running') {
        Stop-Service -Name $ServiceName -Force -Timeout 30
        Start-Sleep -Seconds 2
    }
    
    # Security: Use sc.exe for service removal (more secure than Remove-Service)
    $result = sc.exe delete $ServiceName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to remove existing service: $result"
        exit 1
    }
    Start-Sleep -Seconds 2
}

# Create the service
Write-Host "`nCreating Windows Service..." -ForegroundColor Yellow

$exePath = Join-Path $InstallPath "ClaudeCLIRunner.exe"

# Security: Validate executable exists and is signed (if possible)
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found: $exePath"
    exit 1
}

# Security: Check if executable is signed
try {
    $signature = Get-AuthenticodeSignature $exePath
    if ($signature.Status -ne "Valid") {
        Write-Warning "Executable is not properly signed. This may be a security risk."
        $confirm = Read-Host "Do you want to continue? (y/N)"
        if ($confirm -ne 'y' -and $confirm -ne 'Y') {
            Write-Host "Installation cancelled."
            exit 0
        }
    }
    else {
        Write-Host "Executable signature verified." -ForegroundColor Green
    }
}
catch {
    Write-Warning "Could not verify executable signature: $($_.Exception.Message)"
}

# Create service with secure parameters
$params = @{
    Name = $ServiceName
    BinaryPathName = "`"$exePath`""  # Quote the path for security
    DisplayName = $DisplayName
    Description = $Description
    StartupType = $StartupType
}

try {
    New-Service @params
    Write-Host "Service created successfully." -ForegroundColor Green
}
catch {
    Write-Error "Failed to create service: $($_.Exception.Message)"
    exit 1
}

# Configure service recovery options
Write-Host "Configuring service recovery options..." -ForegroundColor Yellow
$recoveryResult = sc.exe failure $ServiceName reset=86400 actions=restart/60000/restart/60000/restart/60000
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Failed to configure service recovery options: $recoveryResult"
}

# Security: Set service account if specified
if ($ServiceAccount -ne "LocalSystem") {
    Write-Host "Configuring service account: $ServiceAccount" -ForegroundColor Yellow
    
    try {
        # Use WMI to change service account (more secure than sc.exe for this)
        $service = Get-WmiObject -Class Win32_Service -Filter "Name='$ServiceName'"
        $result = $service.Change($null, $null, $null, $null, $null, $null, $ServiceAccount, $null)
        
        if ($result.ReturnValue -eq 0) {
            Write-Host "Service account configured successfully." -ForegroundColor Green
        }
        else {
            Write-Warning "Failed to configure service account. Return code: $($result.ReturnValue)"
        }
    }
    catch {
        Write-Warning "Failed to configure service account: $($_.Exception.Message)"
    }
}

# Security: Set file permissions on installation directory
Write-Host "Setting secure file permissions..." -ForegroundColor Yellow
try {
    $acl = Get-Acl $InstallPath
    $acl.SetAccessRuleProtection($true, $false)  # Remove inherited permissions
    $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")))
    $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("Administrators", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")))
    $acl.SetAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule("Users", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")))
    Set-Acl $InstallPath $acl
    Write-Host "File permissions secured." -ForegroundColor Green
}
catch {
    Write-Warning "Failed to set secure file permissions: $($_.Exception.Message)"
}

Write-Host "`nService installation completed!" -ForegroundColor Green
Write-Host "`nService Details:" -ForegroundColor Cyan
Write-Host "  Name: $ServiceName"
Write-Host "  Display Name: $DisplayName"
Write-Host "  Install Path: $InstallPath"
Write-Host "  Executable: $exePath"
Write-Host "  Startup Type: $StartupType"
Write-Host "  Service Account: $ServiceAccount"

Write-Host "`nSECURITY RECOMMENDATIONS:" -ForegroundColor Yellow
Write-Host "  1. Store credentials in environment variables, not configuration files"
Write-Host "  2. Enable audit logging in the configuration"
Write-Host "  3. Monitor service logs regularly"
Write-Host "  4. Keep the application updated"
Write-Host "  5. Review file permissions periodically"

Write-Host "`nTo start the service, run:" -ForegroundColor Yellow
Write-Host "  Start-Service -Name $ServiceName" -ForegroundColor White

Write-Host "`nTo view service logs, run:" -ForegroundColor Yellow
Write-Host "  Get-EventLog -LogName Application -Source $ServiceName -Newest 20" -ForegroundColor White

Write-Host "`nTo uninstall the service, run:" -ForegroundColor Yellow
Write-Host "  Stop-Service -Name $ServiceName; sc.exe delete $ServiceName" -ForegroundColor White

Write-Host "`nSecurity documentation: docs/SECURITY.md" -ForegroundColor Cyan 