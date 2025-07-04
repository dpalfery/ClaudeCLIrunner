# Security Guide for ClaudeCLIrunner

This document outlines the security features, best practices, and recommendations for deploying and maintaining the ClaudeCLIrunner application securely.

## 🔒 Security Features Implemented

### 1. Input Validation and Sanitization
- **Command Injection Prevention**: All user inputs (work item titles, descriptions) are validated against dangerous patterns
- **Input Length Limits**: 
  - Work item titles: Maximum 1,000 characters
  - Work item descriptions: Maximum 50,000 characters
  - Branch names: Maximum 255 characters
- **Pattern Detection**: Blocks common injection patterns including `|`, `&`, `;`, `$`, `` ` ``, `$(`, `${`, `powershell`, `cmd`, `bash`, etc.
- **Path Validation**: Executable paths are validated to prevent arbitrary code execution

### 2. Secure Process Execution
- **Argument List Security**: Uses `ProcessStartInfo.ArgumentList` instead of concatenated strings to prevent argument injection
- **Environment Variable Validation**: All environment variables are validated before being set
- **Process Isolation**: Child processes run with limited permissions and proper timeout enforcement

### 3. Credential Management
- **Environment Variable Support**: Credentials should be stored in environment variables, not configuration files
- **Deprecated Configuration Warning**: The application warns when PATs are stored in configuration files
- **Secure Credential Sources**: Supports Azure Key Vault and Windows Credential Manager (future enhancement)

### 4. Logging Security
- **Sensitive Data Masking**: Automatically masks tokens, passwords, and keys in log output
- **Log Level Controls**: Debug information containing sensitive data is only logged at Debug level
- **Audit Logging**: Optional audit logging for compliance requirements

### 5. Configuration Security
- **HTTPS Enforcement**: Option to require HTTPS for all external endpoints
- **Input Validation**: Configuration values are validated using data annotations
- **Secure Defaults**: Default configuration uses secure settings

## 🛡️ Security Best Practices

### 1. Credential Storage

#### ✅ Recommended (Secure)
```bash
# Use environment variables
export CLAUDECLI_AZURE_DEVOPS_PAT="your-pat-here"

# Or use Windows Credential Manager
cmdkey /add:ClaudeCLIRunner /user:AzureDevOpsPAT /pass:your-pat-here
```

#### ❌ Not Recommended (Insecure)
```json
// Do NOT store credentials in appsettings.json
{
  "ClaudeCliConfig": {
    "AzureDevOpsPat": "your-pat-here"  // NEVER DO THIS!
  }
}
```

### 2. Configuration Security

#### Secure Configuration Template
```json
{
  "ClaudeCliConfig": {
    "ClaudeCodeCliPath": "/usr/local/bin/claude",
    "McpEndpoint": "https://secure-mcp-server.example.com",
    "RequireHttps": true,
    "MaxConcurrentProcesses": 1,
    "EnableAuditLogging": true,
    "AuditLogPath": "/var/log/claudecli/audit.log",
    "PollIntervalSeconds": 60,
    "MaxRetries": 3,
    "MaxTaskDurationMinutes": 60,
    "WebhookPort": 8443
  }
}
```

### 3. Deployment Security

#### File Permissions
```bash
# Secure configuration files
chmod 640 appsettings.json
chown root:claudecli appsettings.json

# Secure log directory
mkdir -p /var/log/claudecli
chmod 750 /var/log/claudecli
chown claudecli:claudecli /var/log/claudecli
```

#### Service Account
```bash
# Create dedicated service account
useradd -r -s /bin/false claudecli
usermod -L claudecli  # Lock the account
```

### 4. Network Security

#### Firewall Rules
```bash
# Allow only necessary outbound connections
iptables -A OUTPUT -p tcp --dport 443 -j ACCEPT  # HTTPS
iptables -A OUTPUT -p tcp --dport 80 -j ACCEPT   # HTTP (if needed)
iptables -A OUTPUT -j DROP  # Drop all other outbound traffic
```

#### TLS Configuration
- Always use HTTPS endpoints for MCP and Azure DevOps
- Verify SSL certificates
- Use TLS 1.2 or higher

## ⚠️ Security Warnings

### 1. Known Security Considerations
- **Process Execution**: The application executes external processes (Claude CLI), ensure the executable is trusted
- **Work Item Content**: Work items may contain untrusted user input from Azure DevOps
- **Network Communication**: MCP endpoints and Azure DevOps APIs require network access

### 2. Security Limitations
- **Sandbox Limitations**: The application does not run Claude CLI in a full sandbox
- **Resource Limits**: No built-in memory or CPU limits for child processes
- **File System Access**: Claude CLI has access to the file system

## 🔧 Security Configuration Options

### Required Security Settings
```json
{
  "ClaudeCliConfig": {
    "RequireHttps": true,           // Enforce HTTPS for external endpoints
    "MaxConcurrentProcesses": 1,    // Limit concurrent processes
    "EnableAuditLogging": true      // Enable security audit logging
  }
}
```

### Optional Security Enhancements
```json
{
  "ClaudeCliConfig": {
    "AuditLogPath": "/var/log/claudecli/audit.log",  // Custom audit log location
    "MaxTaskDurationMinutes": 30,                    // Shorter timeout
    "PollIntervalSeconds": 120                       // Less frequent polling
  }
}
```

## 🚨 Incident Response

### 1. Security Event Detection
- Monitor audit logs for suspicious activity
- Watch for validation failures in application logs
- Monitor process execution times and failures

### 2. Security Event Response
1. **Immediate**: Stop the service if compromise is suspected
2. **Investigation**: Review audit logs and system logs
3. **Recovery**: Rotate credentials and restart with updated configuration
4. **Prevention**: Update input validation rules if new attack patterns are detected

## 📋 Security Checklist

### Pre-Deployment
- [ ] Credentials stored in environment variables or secure credential store
- [ ] Configuration files do not contain secrets
- [ ] HTTPS endpoints configured for all external services
- [ ] File permissions set correctly on configuration and log files
- [ ] Service account configured with minimal privileges
- [ ] Network access restricted to necessary endpoints only

### Post-Deployment
- [ ] Audit logging enabled and monitored
- [ ] Regular security log reviews scheduled
- [ ] Credential rotation policy established
- [ ] Incident response procedures documented
- [ ] Security updates applied regularly

### Ongoing Maintenance
- [ ] Monitor for new vulnerability disclosures
- [ ] Update dangerous pattern detection rules as needed
- [ ] Review and test incident response procedures
- [ ] Conduct regular security assessments

## 🔗 Additional Security Resources

### Secure Development
- [OWASP Secure Coding Practices](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/)
- [.NET Security Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/security/)

### Azure DevOps Security
- [Azure DevOps Security Best Practices](https://docs.microsoft.com/en-us/azure/devops/organizations/security/)
- [Personal Access Token Security](https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate)

### Windows Service Security
- [Windows Service Security](https://docs.microsoft.com/en-us/windows/win32/services/service-security-and-access-rights)
- [Running Services with Minimal Privileges](https://docs.microsoft.com/en-us/windows/win32/services/service-user-accounts)

## 📞 Security Contact
For security-related questions or to report security vulnerabilities, please contact the development team through secure channels.