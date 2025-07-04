@description('The name of the environment (e.g., dev, staging, prod)')
param environment string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('The name prefix for all resources')
param resourcePrefix string = 'claudecli'

@description('Container image for the Claude CLI Runner')
param containerImage string = 'mcr.microsoft.com/dotnet/aspnet:8.0'

@description('Azure DevOps Organization name')
param azureDevOpsOrg string

@description('Azure DevOps Project name')
param azureDevOpsProject string

@description('Azure DevOps Repository name')
param azureDevOpsRepo string

@description('MCP Endpoint URL')
param mcpEndpoint string

@description('Container restart policy')
@allowed(['Always', 'Never', 'OnFailure'])
param restartPolicy string = 'Always'

// Variables
var resourceName = '${resourcePrefix}-${environment}'
var keyVaultName = '${resourceName}-kv'
var storageAccountName = replace('${resourceName}storage', '-', '')
var logAnalyticsName = '${resourceName}-logs'
var containerGroupName = '${resourceName}-aci'

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// Storage Account for application data and logs
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

// Key Vault for storing secrets
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    accessPolicies: []
    publicNetworkAccess: 'Enabled'
    enableRbacAuthorization: true
  }
}

// User Assigned Managed Identity for Container Instance
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourceName}-identity'
  location: location
}

// Key Vault Secrets User role assignment for managed identity
resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, managedIdentity.id, 'Key Vault Secrets User')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Data Contributor role assignment for managed identity
resource storageBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, managedIdentity.id, 'Storage Blob Data Contributor')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Container Instance for Claude CLI Runner
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerGroupName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    containers: [
      {
        name: 'claudecli-runner'
        properties: {
          image: containerImage
          ports: [
            {
              port: 80
              protocol: 'TCP'
            }
          ]
          resources: {
            requests: {
              cpu: 1
              memoryInGB: 2
            }
            limits: {
              cpu: 2
              memoryInGB: 4
            }
          }
          environmentVariables: [
            {
              name: 'CLAUDECLI_ClaudeCliConfig__AzureDevOpsOrg'
              value: azureDevOpsOrg
            }
            {
              name: 'CLAUDECLI_ClaudeCliConfig__Project'
              value: azureDevOpsProject
            }
            {
              name: 'CLAUDECLI_ClaudeCliConfig__Repo'
              value: azureDevOpsRepo
            }
            {
              name: 'CLAUDECLI_ClaudeCliConfig__McpEndpoint'
              value: mcpEndpoint
            }
            {
              name: 'CLAUDECLI_ClaudeCliConfig__PollIntervalSeconds'
              value: '60'
            }
            {
              name: 'CLAUDECLI_ClaudeCliConfig__MaxRetries'
              value: '3'
            }
            {
              name: 'CLAUDECLI_ClaudeCliConfig__MaxTaskDurationMinutes'
              value: '60'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: managedIdentity.properties.clientId
            }
            {
              name: 'KEYVAULT_URI'
              value: keyVault.properties.vaultUri
            }
            {
              name: 'STORAGE_ACCOUNT_NAME'
              value: storageAccount.name
            }
          ]
        }
      }
    ]
    osType: 'Linux'
    restartPolicy: restartPolicy
    ipAddress: {
      type: 'Public'
      ports: [
        {
          port: 80
          protocol: 'TCP'
        }
      ]
    }
    diagnostics: {
      logAnalytics: {
        workspaceId: logAnalytics.properties.customerId
        workspaceKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
  dependsOn: [
    keyVaultSecretsUserRole
    storageBlobDataContributorRole
  ]
}

// Outputs
output resourceGroupName string = resourceGroup().name
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output storageAccountName string = storageAccount.name
output containerGroupName string = containerGroup.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output logAnalyticsWorkspaceId string = logAnalytics.properties.customerId