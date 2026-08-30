using AIStudio.Provider;
using AIStudio.Provider.HuggingFace;
using AIStudio.Tools.PluginSystem;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Tools.Validation;

public sealed class ProviderValidation
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ProviderValidation).Namespace, nameof(ProviderValidation));
    
    public Func<LLMProviders> GetProvider { get; init; } = () => LLMProviders.NONE;
    
    public Func<string> GetAPIKeyStorageIssue { get; init; } = () => string.Empty;
    
    public Func<string> GetPreviousInstanceName { get; init; } = () => string.Empty;
    
    public Func<IEnumerable<string>> GetUsedInstanceNames { get; init; } = () => [];

    public Func<Host> GetHost { get; init; } = () => Host.NONE;

    public Func<bool> IsModelProvidedManually { get; init; } = () => false;

    public Func<bool> IsModelSelectionHidden { get; init; } = () => false;

    public string? ValidatingHostname(string hostname)
    {
        //
        // Every provider for which IsHostnameNeeded is true must be validated here. Otherwise,
        // the dialog shows a hostname field which nobody checks, and the provider silently ends
        // up as a NoProvider later on, because its base URI cannot be built:
        //
        if(this.GetProvider() is not (LLMProviders.SELF_HOSTED or LLMProviders.LITE_LLM))
            return null;
        
        if(string.IsNullOrWhiteSpace(hostname))
            return TB("Please enter a hostname, e.g., http://localhost:1234");
        
        if(!hostname.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase) && !hostname.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase))
            return TB("The hostname must start with either http:// or https://");

        if(!Uri.TryCreate(hostname, UriKind.Absolute, out _))
            return TB("The hostname is not a valid HTTP(S) URL.");
        
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
            return TB("Please enter an API key.");
        
        return null;
    }

    public string? ValidatingInstanceName(string instanceName)
    {
        if (string.IsNullOrWhiteSpace(instanceName))
            return TB("Please enter an instance name.");
        
        if (instanceName.Length > 40)
            return TB("The instance name must not exceed 40 characters.");
        
        // The instance name must be unique:
        var lowerInstanceName = instanceName.ToLowerInvariant();
        if (lowerInstanceName != this.GetPreviousInstanceName() && this.GetUsedInstanceNames().Contains(lowerInstanceName))
            return TB("The instance name must be unique; the chosen name is already in use.");
        
        return null;
    }

    public string? ValidatingModel(Model model)
    {
        // For NONE providers, no validation is needed:
        if (this.GetProvider() is LLMProviders.NONE)
            return null;

        // For self-hosted whisper.cpp, no model selection needed
        // (model is loaded at startup):
        if (this.GetProvider() is LLMProviders.SELF_HOSTED && this.GetHost() is Host.WHISPER_CPP)
            return null;

        // For legacy hosts without model selection, no selection validation is needed:
        if (this.IsModelSelectionHidden())
            return null;

        // For manually entered models, this validation doesn't apply:
        if (this.IsModelProvidedManually())
            return null;

        if (model == default)
            return TB("Please select a model.");

        return null;
    }

    public string? ValidatingProvider(LLMProviders llmProvider)
    {
        if (llmProvider == LLMProviders.NONE)
            return TB("Please select a provider.");
        
        return null;
    }

    public string? ValidatingHost(Host host)
    {
        if(this.GetProvider() is not LLMProviders.SELF_HOSTED)
            return null;

        if (host == Host.NONE)
            return TB("Please select a host.");

        return null;
    }
    
    public string? ValidatingHFInstanceProvider(HFInferenceProvider inferenceProvider)
    {
        if(this.GetProvider() is not LLMProviders.HUGGINGFACE)
            return null;

        if (!inferenceProvider.SupportsChat())
            return TB("Please select an Hugging Face inference provider.");

        return null;
    }

    /// <summary>
    /// Validates the Hugging Face inference provider chosen for embeddings.
    /// </summary>
    /// <remarks>
    /// Far fewer providers create embeddings for us than serve chat models, so a selection which is
    /// fine for chatting may not be for embeddings. A provider configured before the choice narrowed
    /// is no longer among the options, which would leave the user with an empty field and no reason
    /// given.
    /// </remarks>
    /// <param name="inferenceProvider">The inference provider to validate.</param>
    /// <returns>The message to show, or null when the selection is fine.</returns>
    public string? ValidatingHFInstanceProviderForEmbeddings(HFInferenceProvider inferenceProvider)
    {
        if(this.GetProvider() is not LLMProviders.HUGGINGFACE)
            return null;

        if (inferenceProvider is HFInferenceProvider.NONE)
            return TB("Please select an Hugging Face inference provider.");

        if (!inferenceProvider.SupportsEmbeddings())
            return TB("This Hugging Face inference provider does not create embeddings. Please select another one.");

        return null;
    }

    /// <summary>
    /// Validates the Hugging Face inference provider chosen for transcription.
    /// </summary>
    /// <remarks>
    /// As with embeddings, only some of the inference providers transcribe audio for us, so the
    /// choice is narrower than it is for chatting.
    /// </remarks>
    /// <param name="inferenceProvider">The inference provider to validate.</param>
    /// <returns>The message to show, or null when the selection is fine.</returns>
    public string? ValidatingHFInstanceProviderForTranscription(HFInferenceProvider inferenceProvider)
    {
        if(this.GetProvider() is not LLMProviders.HUGGINGFACE)
            return null;

        if (inferenceProvider is HFInferenceProvider.NONE)
            return TB("Please select an Hugging Face inference provider.");

        if (!inferenceProvider.SupportsTranscription())
            return TB("This Hugging Face inference provider does not transcribe audio. Please select another one.");

        return null;
    }
}