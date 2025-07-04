# Security Audit Script for ClaudeCLIrunner
# This script performs basic security checks and vulnerability scanning

param(
    [switch]$IncludeNuGetAudit,
    [switch]$CheckFilePermissions,
    [switch]$Verbose
)

Write-Host "ClaudeCLIrunner Security Audit" -ForegroundColor Green
Write-Host "==============================" -ForegroundColor Green
Write-Host ""

# Function to write colored output
function Write-SecurityResult {
    param($Message, $Status)
    
    $color = switch ($Status) {
        "PASS" { "Green" }
        "WARN" { "Yellow" }
        "FAIL" { "Red" }
        "INFO" { "Cyan" }
        default { "White" }
    }
    
    Write-Host "[$Status] $Message" -ForegroundColor $color
}

# Check 1: Configuration Security
Write-Host "1. Configuration Security Checks" -ForegroundColor Yellow
Write-Host "---------------------------------" -ForegroundColor Yellow

$configFile = "ClaudeCLIRunner/appsettings.json"
if (Test-Path $configFile) {
    $config = Get-Content $configFile | ConvertFrom-Json
    
    # Check for hardcoded credentials
    if ($config.ClaudeCliConfig.AzureDevOpsPat) {
        Write-SecurityResult "Hardcoded PAT found in configuration" "FAIL"
        Write-Host "  RECOMMENDATION: Remove PAT from config and use environment variable CLAUDECLI_AZURE_DEVOPS_PAT" -ForegroundColor Red
    } else {
        Write-SecurityResult "No hardcoded PAT in configuration" "PASS"
    }
    
    # Check HTTPS enforcement
    if ($config.ClaudeCliConfig.RequireHttps -eq $true) {
        Write-SecurityResult "HTTPS enforcement enabled" "PASS"
    } else {
        Write-SecurityResult "HTTPS enforcement disabled" "WARN"
        Write-Host "  RECOMMENDATION: Enable RequireHttps for production environments" -ForegroundColor Yellow
    }
    
    # Check audit logging
    if ($config.ClaudeCliConfig.EnableAuditLogging -eq $true) {
        Write-SecurityResult "Audit logging enabled" "PASS"
    } else {
        Write-SecurityResult "Audit logging disabled" "WARN"
        Write-Host "  RECOMMENDATION: Enable audit logging for security monitoring" -ForegroundColor Yellow
    }
    
    # Check endpoint security
    $endpoint = $config.ClaudeCliConfig.McpEndpoint
    if ($endpoint -and $endpoint.StartsWith("https://")) {
        Write-SecurityResult "MCP endpoint uses HTTPS" "PASS"
    } elseif ($endpoint -and $endpoint.StartsWith("http://")) {
        Write-SecurityResult "MCP endpoint uses insecure HTTP" "WARN"
        Write-Host "  RECOMMENDATION: Use HTTPS endpoint for production" -ForegroundColor Yellow
    } else {
        Write-SecurityResult "MCP endpoint configuration unclear" "INFO"
    }
} else {
    Write-SecurityResult "Configuration file not found: $configFile" "WARN"
}

Write-Host ""

# Check 2: Dependency Security
Write-Host "2. Dependency Security Checks" -ForegroundColor Yellow
Write-Host "------------------------------" -ForegroundColor Yellow

if ($IncludeNuGetAudit) {
    Write-Host "Running NuGet security audit..." -ForegroundColor Cyan
    
    # Check if dotnet is available
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        try {
            # Run dotnet list package --vulnerable
            $auditResult = dotnet list package --vulnerable 2>&1
            
            if ($auditResult -match "no vulnerable packages") {
                Write-SecurityResult "No vulnerable NuGet packages found" "PASS"
            } elseif ($auditResult -match "vulnerable") {
                Write-SecurityResult "Vulnerable packages detected" "FAIL"
                Write-Host "  DETAILS:" -ForegroundColor Red
                $auditResult | Where-Object { $_ -match ">" } | ForEach-Object {
                    Write-Host "    $_" -ForegroundColor Red
                }
                Write-Host "  RECOMMENDATION: Update vulnerable packages using 'dotnet add package <package-name>'" -ForegroundColor Red
            } else {
                Write-SecurityResult "Unable to determine package vulnerability status" "WARN"
                if ($Verbose) {
                    Write-Host "  Output: $auditResult" -ForegroundColor Gray
                }
            }
        }
        catch {
            Write-SecurityResult "Error running package audit: $($_.Exception.Message)" "WARN"
        }
    } else {
        Write-SecurityResult ".NET CLI not found - cannot check package vulnerabilities" "WARN"
        Write-Host "  RECOMMENDATION: Install .NET SDK to enable package vulnerability scanning" -ForegroundColor Yellow
    }
} else {
    Write-SecurityResult "Dependency audit skipped (use -IncludeNuGetAudit to enable)" "INFO"
    Write-Host "  COMMAND: dotnet list package --vulnerable" -ForegroundColor Cyan
}

Write-Host ""

# Check 3: File Permissions (Windows/PowerShell specific)
if ($CheckFilePermissions -and $IsWindows) {
    Write-Host "3. File Permission Security Checks" -ForegroundColor Yellow
    Write-Host "-----------------------------------" -ForegroundColor Yellow
    
    $criticalFiles = @(
        "ClaudeCLIRunner/appsettings.json",
        "ClaudeCLIRunner/appsettings.Production.json"
    )
    
    foreach ($file in $criticalFiles) {
        if (Test-Path $file) {
            try {
                $acl = Get-Acl $file
                $everyone = $acl.AccessToString | Select-String "Everyone"
                
                if ($everyone) {
                    Write-SecurityResult "File $file accessible by Everyone group" "WARN"
                    Write-Host "  RECOMMENDATION: Restrict file permissions to administrators only" -ForegroundColor Yellow
                } else {
                    Write-SecurityResult "File $file has restricted permissions" "PASS"
                }
            }
            catch {
                Write-SecurityResult "Cannot check permissions for $file" "WARN"
            }
        }
    }
} elseif ($CheckFilePermissions) {
    Write-SecurityResult "File permission checks only supported on Windows" "INFO"
} else {
    Write-SecurityResult "File permission checks skipped (use -CheckFilePermissions to enable)" "INFO"
}

Write-Host ""

# Check 4: Environment Variables
Write-Host "4. Environment Variable Security" -ForegroundColor Yellow
Write-Host "--------------------------------" -ForegroundColor Yellow

$secureEnvVars = @(
    "CLAUDECLI_AZURE_DEVOPS_PAT",
    "CLAUDECLI_ClaudeCliConfig__AzureDevOpsPat"
)

$foundSecureVars = 0
foreach ($envVar in $secureEnvVars) {
    if ([Environment]::GetEnvironmentVariable($envVar)) {
        $foundSecureVars++
        Write-SecurityResult "Secure credential environment variable found: $envVar" "PASS"
    }
}

if ($foundSecureVars -eq 0) {
    Write-SecurityResult "No secure credential environment variables found" "WARN"
    Write-Host "  RECOMMENDATION: Set CLAUDECLI_AZURE_DEVOPS_PAT environment variable" -ForegroundColor Yellow
}

Write-Host ""

# Check 5: Security Documentation
Write-Host "5. Security Documentation" -ForegroundColor Yellow
Write-Host "-------------------------" -ForegroundColor Yellow

$securityDocs = @(
    "docs/SECURITY.md",
    "README.md"
)

foreach ($doc in $securityDocs) {
    if (Test-Path $doc) {
        Write-SecurityResult "Security documentation found: $doc" "PASS"
    } else {
        Write-SecurityResult "Security documentation missing: $doc" "WARN"
    }
}

Write-Host ""

# Summary
Write-Host "Security Audit Summary" -ForegroundColor Green
Write-Host "======================" -ForegroundColor Green
Write-Host "For complete security guidelines, see: docs/SECURITY.md" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Address any FAIL or WARN items above" -ForegroundColor White
Write-Host "2. Run with -IncludeNuGetAudit to check for vulnerable dependencies" -ForegroundColor White
Write-Host "3. Run with -CheckFilePermissions to verify file security" -ForegroundColor White
Write-Host "4. Review and implement security recommendations from docs/SECURITY.md" -ForegroundColor White
Write-Host "5. Consider setting up automated security monitoring" -ForegroundColor White