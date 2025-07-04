# Claude CLI Runner - Infrastructure

This folder contains the Azure infrastructure as code (IaC) templates using Bicep to deploy the Claude CLI Runner application to Azure.

## Infrastructure Components

The infrastructure consists of the following Azure resources:

- **Container Instance (ACI)**: Hosts the Claude CLI Runner application
- **Key Vault**: Stores secrets like Azure DevOps PAT and API keys
- **Storage Account**: Used for application data and logs
- **Log Analytics Workspace**: Centralized logging and monitoring
- **Managed Identity**: Provides secure access to Azure resources without storing credentials

## Files Structure

```
infrastructure/
├── main.bicep                  # Main Bicep template
├── parameters/
│   ├── dev.bicepparam         # Development environment parameters
│   └── prod.bicepparam        # Production environment parameters
└── README.md                  # This file
```

## Prerequisites

1. **Azure Subscription**: You need an active Azure subscription
2. **Azure DevOps Service Connection**: Set up a service connection in Azure DevOps that can deploy to your Azure subscription
3. **Variable Groups**: Create variable groups in Azure DevOps for each environment
4. **Bicep CLI**: The pipeline will install this automatically

## Variable Groups Setup

### Development Environment (`claude-cli-dev`)
Create a variable group named `claude-cli-dev` with the following variables:

| Variable Name | Description | Example Value |
|---------------|-------------|---------------|
| `azureDevOpsOrg` | Your Azure DevOps organization name | `myorg` |
| `azureDevOpsProject` | Your Azure DevOps project name | `myproject` |
| `azureDevOpsRepo` | Your repository name | `myrepo` |
| `mcpEndpoint` | MCP server endpoint URL | `https://your-mcp-server.com` |

### Production Environment (`claude-cli-prod`)
Create a variable group named `claude-cli-prod` with the following variables:

| Variable Name | Description | Example Value |
|---------------|-------------|---------------|
| `containerImage` | Production container image | `myregistry.azurecr.io/claudecli:latest` |
| `azureDevOpsOrg` | Your Azure DevOps organization name | `myorg` |
| `azureDevOpsProject` | Your Azure DevOps project name | `myproject` |
| `azureDevOpsRepo` | Your repository name | `myrepo` |
| `mcpEndpoint` | MCP server endpoint URL | `https://your-mcp-server.com` |

## Pipeline Configuration

1. **Update Service Connection**: In `azure-pipelines-infrastructure.yml`, update the `azureServiceConnectionName` variable to match your Azure service connection name.

2. **Set up Environments**: Create environments in Azure DevOps:
   - `development` - for dev deployments
   - `production` - for prod deployments (add approval gates as needed)

## Deployment Process

### Automatic Deployments
- **Development**: Triggered on pushes to the `develop` branch
- **Production**: Triggered on pushes to the `main` branch
- **Validation**: Runs on all pull requests to `main`

### Manual Deployment
You can also deploy manually using Azure CLI:

```bash
# Login to Azure
az login

# Create resource group (if it doesn't exist)
az group create --name rg-claudecli-dev --location "East US"

# Deploy the infrastructure
az deployment group create \
  --resource-group rg-claudecli-dev \
  --template-file infrastructure/main.bicep \
  --parameters infrastructure/parameters/dev.bicepparam
```

## Post-Deployment Configuration

After the infrastructure is deployed, you need to add secrets to the Key Vault:

### Required Secrets
Add these secrets to the deployed Key Vault:

1. **Azure DevOps PAT**: 
   ```bash
   az keyvault secret set \
     --vault-name "claudecli-dev-kv" \
     --name "AzureDevOpsPAT" \
     --value "your-azure-devops-pat-token"
   ```

2. **Claude API Key** (if needed):
   ```bash
   az keyvault secret set \
     --vault-name "claudecli-dev-kv" \
     --name "ClaudeApiKey" \
     --value "your-claude-api-key"
   ```

### Update Application Configuration
The application will automatically receive the following environment variables:
- `AZURE_CLIENT_ID`: Managed identity client ID
- `KEYVAULT_URI`: Key Vault URI for accessing secrets
- `STORAGE_ACCOUNT_NAME`: Storage account name for logs/data

## Monitoring and Logging

- **Container Logs**: View logs in Azure Container Instances
- **Application Insights**: Integrated with Log Analytics workspace
- **Azure Monitor**: Set up alerts for container health and performance

## Security Considerations

1. **Managed Identity**: The container uses a managed identity to access Azure resources
2. **Key Vault**: All secrets are stored securely in Azure Key Vault
3. **Network Security**: Consider adding network restrictions if needed
4. **RBAC**: The managed identity has minimal required permissions

## Troubleshooting

### Common Issues

1. **Bicep Validation Errors**: Check the template syntax and parameter values
2. **Permission Errors**: Ensure the service connection has sufficient permissions
3. **Resource Naming**: Azure resource names must be globally unique (especially storage accounts)

### Debugging Steps

1. Check the pipeline logs in Azure DevOps
2. Verify the deployment in the Azure portal
3. Check container logs for application issues
4. Validate Key Vault access and secrets

## Cost Optimization

- Container instances are billed per second of execution
- Storage accounts use standard tier with hot access tier
- Log Analytics charges based on data ingestion
- Key Vault has per-operation and per-secret costs

Consider adjusting resource sizes based on your actual usage patterns.

## Customization

You can customize the infrastructure by:

1. **Modifying Bicep Templates**: Update `main.bicep` for additional resources
2. **Environment-Specific Parameters**: Adjust parameter files for different configurations
3. **Scaling**: Modify container resource limits in the template
4. **Additional Environments**: Add more parameter files and pipeline stages