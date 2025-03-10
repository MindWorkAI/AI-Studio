using AIStudio.Settings.DataModel;
using AIStudio.Tools.ERIClient.DataModel;

namespace AIStudio.Tools.Validation;

public sealed class DataSourceValidation
{
    public Func<string> GetSecretStorageIssue { get; init; } = () => string.Empty;
    
    public Func<string> GetPreviousDataSourceName { get; init; } = () => string.Empty;
    
    public Func<IEnumerable<string>> GetUsedDataSourceNames { get; init; } = () => [];
    
    public Func<AuthMethod> GetAuthMethod { get; init; } = () => AuthMethod.NONE;

    public Func<SecurityRequirements?> GetSecurityRequirements { get; init; } = () => null;
    
    public Func<bool> GetSelectedCloudEmbedding { get; init; } = () => false;
    
    public Func<bool> GetTestedConnection { get; init; } = () => false;
    
    public Func<bool> GetTestedConnectionResult { get; init; } = () => false;
    
    public Func<IReadOnlyList<AuthMethod>> GetAvailableAuthMethods { get; init; } = () => [];
    
    public string? ValidatingHostname(string hostname)
    {
        if(string.IsNullOrWhiteSpace(hostname))
            return "Please enter a hostname, e.g., http://localhost";
        
        if(!hostname.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !hostname.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
            return "The hostname must start with either http:// or https://";

        if(!Uri.TryCreate(hostname, UriKind.Absolute, out _))
            return "The hostname is not a valid HTTP(S) URL.";
        
        return null;
    }

    public string? ValidatePort(int port)
    {
        if(port is < 1 or > 65535)
            return "The port must be between 1 and 65535.";
        
        return null;
    }
    
    public string? ValidateSecurityPolicy(DataSourceSecurity securityPolicy)
    {
        if(securityPolicy is DataSourceSecurity.NOT_SPECIFIED)
            return "Please select your security policy.";
        
        var dataSourceSecurity = this.GetSecurityRequirements();
        if (dataSourceSecurity is null)
            return null;
        
        if(dataSourceSecurity.Value.AllowedProviderType is ProviderType.SELF_HOSTED && securityPolicy is not DataSourceSecurity.SELF_HOSTED)
            return "This data source can only be used with a self-hosted LLM provider. Please change the security policy.";
        
        return null;
    }
    
    public string? ValidateUsername(string username)
    {
        if(this.GetAuthMethod() is not AuthMethod.USERNAME_PASSWORD)
            return null;
        
        if(string.IsNullOrWhiteSpace(username))
            return "The username must not be empty.";
        
        return null;
    }
    
    public string? ValidatingSecret(string secret)
    {
        var authMethod = this.GetAuthMethod();
        if(authMethod is AuthMethod.NONE or AuthMethod.KERBEROS)
            return null;
        
        var secretStorageIssue = this.GetSecretStorageIssue();
        if(!string.IsNullOrWhiteSpace(secretStorageIssue))
            return secretStorageIssue;

        if (string.IsNullOrWhiteSpace(secret))
            return authMethod switch
            {
                AuthMethod.TOKEN => "Please enter your secure access token.",
                AuthMethod.USERNAME_PASSWORD => "Please enter your password.",

                _ => "Please enter the secret necessary for authentication."
            };
        
        return null;
    }

    public string? ValidateRetrievalProcess(RetrievalInfo retrievalInfo)
    {
        if(retrievalInfo == default)
            return "Please select one retrieval process.";
        
        return null;
    }
    
    public string? ValidatingName(string dataSourceName)
    {
        if(string.IsNullOrWhiteSpace(dataSourceName))
            return "The name must not be empty.";
        
        if (dataSourceName.Length > 40)
            return "The name must not exceed 40 characters.";
        
        var lowerName = dataSourceName.ToLowerInvariant();
        if(lowerName != this.GetPreviousDataSourceName() && this.GetUsedDataSourceNames().Contains(lowerName))
            return "The name is already used by another data source. Please choose a different name.";
        
        return null;
    }

    public string? ValidatePath(string path)
    {
        if(string.IsNullOrWhiteSpace(path))
            return "The path must not be empty. Please select a directory.";
        
        if(!Directory.Exists(path))
            return "The path does not exist. Please select a valid directory.";
        
        return null;
    }

    public string? ValidateFilePath(string filePath)
    {
        if(string.IsNullOrWhiteSpace(filePath))
            return "The file path must not be empty. Please select a file.";
        
        if(!File.Exists(filePath))
            return "The file does not exist. Please select a valid file.";
        
        return null;
    }

    public string? ValidateEmbeddingId(string embeddingId)
    {
        if(string.IsNullOrWhiteSpace(embeddingId))
            return "Please select an embedding provider.";
        
        return null;
    }

    public string? ValidateUserAcknowledgedCloudEmbedding(bool value)
    {
        if(this.GetSelectedCloudEmbedding() && !value)
            return "Please acknowledge that you are aware of the cloud embedding implications.";
        
        return null;
    }

    public string? ValidateTestedConnection()
    {
        if(!this.GetTestedConnection())
            return "Please test the connection before saving.";
        
        if(!this.GetTestedConnectionResult())
            return "The connection test failed. Please check the connection settings.";
        
        return null;
    }
    
    public string? ValidateAuthMethod(AuthMethod authMethod)
    {
        if(!this.GetAvailableAuthMethods().Contains(authMethod))
            return "Please select one valid authentication method.";
        
        return null;
    }
}