// This template can be used to populate Key Vault with required secrets
// Run this after the main infrastructure deployment

@description('The name of the Key Vault')
param keyVaultName string

@description('Azure DevOps Personal Access Token')
@secure()
param azureDevOpsPat string

@description('Claude API Key (if required)')
@secure()
param claudeApiKey string = ''

// Reference to existing Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Azure DevOps PAT Secret
resource azureDevOpsPatSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureDevOpsPAT'
  properties: {
    value: azureDevOpsPat
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Claude API Key Secret (optional)
resource claudeApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (claudeApiKey != '') {
  parent: keyVault
  name: 'ClaudeApiKey'
  properties: {
    value: claudeApiKey
    contentType: 'text/plain'
    attributes: {
      enabled: true
    }
  }
}

// Outputs
output azureDevOpsPatSecretUri string = azureDevOpsPatSecret.properties.secretUri
output claudeApiKeySecretUri string = claudeApiKey != '' ? claudeApiKeySecret.properties.secretUri : ''