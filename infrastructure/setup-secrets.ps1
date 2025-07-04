# Setup script for Claude CLI Runner infrastructure secrets
# Run this script after deploying the main infrastructure

param(
    [Parameter(Mandatory=$true)]
    [string]$KeyVaultName,
    
    [Parameter(Mandatory=$true)]
    [string]$AzureDevOpsPat,
    
    [Parameter(Mandatory=$false)]
    [string]$ClaudeApiKey = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = ""
)

Write-Host "Setting up secrets in Key Vault: $KeyVaultName" -ForegroundColor Green

try {
    # Check if user is logged in to Azure
    $context = Get-AzContext
    if (-not $context) {
        Write-Host "Please login to Azure first using Connect-AzAccount" -ForegroundColor Red
        exit 1
    }

    Write-Host "Current Azure context: $($context.Account.Id)" -ForegroundColor Yellow

    # Verify Key Vault exists
    $keyVault = Get-AzKeyVault -VaultName $KeyVaultName
    if (-not $keyVault) {
        Write-Host "Key Vault '$KeyVaultName' not found. Please ensure the infrastructure is deployed." -ForegroundColor Red
        exit 1
    }

    Write-Host "Found Key Vault: $($keyVault.VaultName)" -ForegroundColor Green

    # Set Azure DevOps PAT secret
    Write-Host "Setting Azure DevOps PAT secret..." -ForegroundColor Yellow
    $azureDevOpsPatSecure = ConvertTo-SecureString -String $AzureDevOpsPat -AsPlainText -Force
    Set-AzKeyVaultSecret -VaultName $KeyVaultName -Name "AzureDevOpsPAT" -SecretValue $azureDevOpsPatSecure -ContentType "text/plain"
    Write-Host "✓ Azure DevOps PAT secret set successfully" -ForegroundColor Green

    # Set Claude API Key secret (if provided)
    if ($ClaudeApiKey -ne "") {
        Write-Host "Setting Claude API Key secret..." -ForegroundColor Yellow
        $claudeApiKeySecure = ConvertTo-SecureString -String $ClaudeApiKey -AsPlainText -Force
        Set-AzKeyVaultSecret -VaultName $KeyVaultName -Name "ClaudeApiKey" -SecretValue $claudeApiKeySecure -ContentType "text/plain"
        Write-Host "✓ Claude API Key secret set successfully" -ForegroundColor Green
    }

    # Display summary
    Write-Host "`nSetup Summary:" -ForegroundColor Cyan
    Write-Host "Key Vault: $KeyVaultName" -ForegroundColor White
    Write-Host "Secrets configured:" -ForegroundColor White
    Write-Host "  - AzureDevOpsPAT: ✓" -ForegroundColor Green
    if ($ClaudeApiKey -ne "") {
        Write-Host "  - ClaudeApiKey: ✓" -ForegroundColor Green
    }

    # If resource group provided, restart container instance
    if ($ResourceGroupName -ne "") {
        Write-Host "`nRestarting container instance to pick up new secrets..." -ForegroundColor Yellow
        $containerGroups = Get-AzContainerGroup -ResourceGroupName $ResourceGroupName
        foreach ($cg in $containerGroups) {
            if ($cg.Name -like "*claudecli*") {
                Write-Host "Restarting container group: $($cg.Name)" -ForegroundColor Yellow
                Restart-AzContainerGroup -ResourceGroupName $ResourceGroupName -Name $cg.Name
                Write-Host "✓ Container group restarted" -ForegroundColor Green
            }
        }
    }

    Write-Host "`nSecrets setup completed successfully! 🎉" -ForegroundColor Green
    Write-Host "The Claude CLI Runner should now be able to access the required secrets." -ForegroundColor White

} catch {
    Write-Host "Error occurred during setup: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Example usage comments
Write-Host "`n" -NoNewline
Write-Host "Example usage:" -ForegroundColor Cyan
Write-Host ".\setup-secrets.ps1 -KeyVaultName 'claudecli-dev-kv' -AzureDevOpsPat 'your-pat-token' -ResourceGroupName 'rg-claudecli-dev'" -ForegroundColor Gray