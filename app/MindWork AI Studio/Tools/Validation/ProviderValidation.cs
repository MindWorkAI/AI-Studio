using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tools.Validation;

public sealed class ProviderValidation
{
    public Func<LLMProviders> GetProvider { get; init; } = () => LLMProviders.NONE;
    
    public Func<string> GetAPIKeyStorageIssue { get; init; } = () => string.Empty;
    
    public Func<string> GetPreviousInstanceName { get; init; } = () => string.Empty;
    
    public Func<IEnumerable<string>> GetUsedInstanceNames { get; init; } = () => [];

    public Func<Host> GetHost { get; init; } = () => Host.NONE;
    
    public string? ValidatingHostname(string hostname)
    {
        if(this.GetProvider() != LLMProviders.SELF_HOSTED)
            return null;
        
        if(string.IsNullOrWhiteSpace(hostname))
            return "Please enter a hostname, e.g., http://localhost:1234";
        
        if(!hostname.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !hostname.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
            return "The hostname must start with either http:// or https://";

        if(!Uri.TryCreate(hostname, UriKind.Absolute, out _))
            return "The hostname is not a valid HTTP(S) URL.";
        
        return null;
    }

    public string? ValidatingAPIKey(string apiKey)
    {
        if(this.GetProvider() is LLMProviders.SELF_HOSTED)
            return null;
        
        var apiKeyStorageIssue = this.GetAPIKeyStorageIssue();
        if(!string.IsNullOrWhiteSpace(apiKeyStorageIssue))
            return apiKeyStorageIssue;

        if(string.IsNullOrWhiteSpace(apiKey))
            return "Please enter an API key.";
        
        return null;
    }

    public string? ValidatingInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return "Please enter an instance name.";
        
        if (instanceName.Length > 40)
            return "The instance name must not exceed 40 characters.";
        
        // The instance name must be unique:
        var lowerInstanceName = instanceName.ToLowerInvariant();
        if (lowerInstanceName != this.GetPreviousInstanceName() && this.GetUsedInstanceNames().Contains(lowerInstanceName))
            return "The instance name must be unique; the chosen name is already in use.";
        
        return null;
    }

    public string? ValidatingModel(Model model)
    {
        if(this.GetProvider() is LLMProviders.SELF_HOSTED && this.GetHost() == Host.LLAMACPP)
            return null;
        
        if (model == default)
            return "Please select a model.";
        
        return null;
    }

    public string? ValidatingProvider(LLMProviders llmProvider)
    {
        if (llmProvider == LLMProviders.NONE)
            return "Please select a provider.";
        
        return null;
    }

    public string? ValidatingHost(Host host)
    {
        if(this.GetProvider() is not LLMProviders.SELF_HOSTED)
            return null;

        if (host == Host.NONE)
            return "Please select a host.";

        return null;
    }
    
    public string? ValidatingHFInstanceProvider(HFInstanceProvider instanceProvider)
    {
        if(this.GetProvider() is not LLMProviders.HUGGINGFACE)
            return null;

        if (instanceProvider is HFInstanceProvider.NONE)
            return "Please select an Hugging Face instance provider.";

        return null;
    }
}