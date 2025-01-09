using ERI_Client.V1;

namespace AIStudio.Tools.Validation;

public sealed class DataSourceValidation
{
    public Func<string> GetSecretStorageIssue { get; init; } = () => string.Empty;
    
    public Func<string> GetPreviousDataSourceName { get; init; } = () => string.Empty;
    
    public Func<IEnumerable<string>> GetUsedDataSourceNames { get; init; } = () => [];
    
    public Func<AuthMethod> GetAuthMethod { get; init; } = () => AuthMethod.NONE;
    
    public Func<bool> GetSelectedCloudEmbedding { get; init; } = () => false;
    
    public string? ValidatingHostname(string hostname)
    {
        if(string.IsNullOrWhiteSpace(hostname))
            return "Please enter a hostname, e.g., http://localhost:1234";
        
        if(!hostname.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !hostname.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
            return "The hostname must start with either http:// or https://";

        if(!Uri.TryCreate(hostname, UriKind.Absolute, out _))
            return "The hostname is not a valid HTTP(S) URL.";
        
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
                AuthMethod.TOKEN => "Please enter your secure token.",
                AuthMethod.USERNAME_PASSWORD => "Please enter your password.",

                _ => "Please enter the secret necessary for authentication."
            };
        
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
}