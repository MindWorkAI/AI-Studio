using AIStudio.Provider.AlibabaCloud;
using AIStudio.Provider.Anthropic;
using AIStudio.Provider.DeepSeek;
using AIStudio.Provider.Fireworks;
using AIStudio.Provider.Google;
using AIStudio.Provider.Groq;
using AIStudio.Provider.GWDG;
using AIStudio.Provider.Helmholtz;
using AIStudio.Provider.HuggingFace;
using AIStudio.Provider.Mistral;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.SelfHosted;
using AIStudio.Provider.X;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Provider;

public static class LLMProvidersExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(LLMProvidersExtensions).Namespace, nameof(LLMProvidersExtensions));
    
    /// <summary>
    /// Returns the human-readable name of the provider.
    /// </summary>
    /// <param name="llmProvider">The provider.</param>
    /// <returns>The human-readable name of the provider.</returns>
    public static string ToName(this LLMProviders llmProvider) => llmProvider switch
    {
        LLMProviders.NONE => TB("No provider selected"),
        
        LLMProviders.OPEN_AI => "OpenAI",
        LLMProviders.ANTHROPIC => "Anthropic",
        LLMProviders.MISTRAL => "Mistral",
        LLMProviders.GOOGLE => "Google",
        LLMProviders.X => "xAI",
        LLMProviders.DEEP_SEEK => "DeepSeek",
        LLMProviders.ALIBABA_CLOUD => "Alibaba Cloud",
        
        LLMProviders.GROQ => "Groq",
        LLMProviders.FIREWORKS => "Fireworks.ai",
        LLMProviders.HUGGINGFACE => "Hugging Face",
        
        LLMProviders.SELF_HOSTED => TB("Self-hosted"),
        
        LLMProviders.HELMHOLTZ => "Helmholtz Blablador",
        LLMProviders.GWDG => "GWDG SAIA",
        
        _ => TB("Unknown"),
    };
    
    /// <summary>
    /// Get a provider's confidence.
    /// </summary>
    /// <param name="llmProvider">The provider.</param>
    /// <param name="settingsManager">The settings manager.</param>
    /// <returns>The confidence of the provider.</returns>
    public static Confidence GetConfidence(this LLMProviders llmProvider, SettingsManager settingsManager) => llmProvider switch
    {
        LLMProviders.NONE => Confidence.NONE,
        
        LLMProviders.FIREWORKS => Confidence.USA_HUB.WithRegion("America, U.S.").WithSources("https://fireworks.ai/terms-of-service").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        // Not trusted, because huggingface only routes you to a third-party-provider and we can't make sure they do not use your data 
        LLMProviders.HUGGINGFACE => Confidence.USA_HUB.WithRegion("America, U.S.").WithSources("https://huggingface.co/terms-of-service").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)), 
        
        LLMProviders.OPEN_AI => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources(
            "https://platform.openai.com/docs/models/default-usage-policies-by-endpoint",
            "https://openai.com/policies/terms-of-use/",
            "https://help.openai.com/en/articles/5722486-how-your-data-is-used-to-improve-model-performance",
            "https://openai.com/enterprise-privacy/"
        ).WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.GOOGLE => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources("https://ai.google.dev/gemini-api/terms").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.GROQ => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources("https://wow.groq.com/terms-of-use/").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.ANTHROPIC => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources("https://www.anthropic.com/legal/commercial-terms").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.MISTRAL => Confidence.GDPR_NO_TRAINING.WithRegion("Europe, France").WithSources("https://mistral.ai/terms/#terms-of-service-la-plateforme").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.X => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources("https://x.ai/legal/terms-of-service-enterprise").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.DEEP_SEEK => Confidence.CHINA_NO_TRAINING.WithRegion("Asia").WithSources("https://cdn.deepseek.com/policies/en-US/deepseek-open-platform-terms-of-service.html").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.ALIBABA_CLOUD => Confidence.CHINA_NO_TRAINING.WithRegion("Asia").WithSources("https://www.alibabacloud.com/help/en/model-studio/support/faq-about-alibaba-cloud-model-studio").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.SELF_HOSTED => Confidence.SELF_HOSTED.WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.HELMHOLTZ => Confidence.GDPR_NO_TRAINING.WithRegion("Europe, Germany").WithSources("https://helmholtz.cloud/services/?serviceID=d7d5c597-a2f6-4bd1-b71e-4d6499d98570").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        LLMProviders.GWDG => Confidence.GDPR_NO_TRAINING.WithRegion("Europe, Germany").WithSources("https://docs.hpc.gwdg.de/services/chat-ai/data-privacy/index.html").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        _ => Confidence.UNKNOWN.WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
    };

    /// <summary>
    /// Determines if the specified provider supports embeddings.
    /// </summary>
    /// <param name="llmProvider">The provider to check.</param>
    /// <returns>True if the provider supports embeddings; otherwise, false.</returns>
    public static bool ProvideEmbeddings(this LLMProviders llmProvider) => llmProvider switch
    {
        //
        // Providers that support embeddings:
        //
        LLMProviders.OPEN_AI => true,
        LLMProviders.MISTRAL => true,
        LLMProviders.GOOGLE => true,
        LLMProviders.HELMHOLTZ => true,
        LLMProviders.ALIBABA_CLOUD => true,
        
        //
        // Providers that do not support embeddings:
        //
        LLMProviders.GROQ => false,
        LLMProviders.ANTHROPIC => false,
        LLMProviders.FIREWORKS => false,
        LLMProviders.X => false,
        LLMProviders.GWDG => false,
        LLMProviders.DEEP_SEEK => false,
        LLMProviders.HUGGINGFACE => false,
        
        //
        // Self-hosted providers are treated as a special case anyway.
        //
        LLMProviders.SELF_HOSTED => true,
        
        _ => false,
    };

    /// <summary>
    /// Creates a new provider instance based on the provider value.
    /// </summary>
    /// <param name="providerSettings">The provider settings.</param>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The provider instance.</returns>
    public static IProvider CreateProvider(this AIStudio.Settings.Provider providerSettings, ILogger logger)
    {
        return providerSettings.UsedLLMProvider.CreateProvider(providerSettings.InstanceName, providerSettings.Host, providerSettings.Hostname, providerSettings.Model, providerSettings.HFInferenceProvider ,logger);
    }
    
    /// <summary>
    /// Creates a new provider instance based on the embedding provider value.
    /// </summary>
    /// <param name="embeddingProviderSettings">The embedding provider settings.</param>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The provider instance.</returns>
    public static IProvider CreateProvider(this EmbeddingProvider embeddingProviderSettings, ILogger logger)
    {
        return embeddingProviderSettings.UsedLLMProvider.CreateProvider(embeddingProviderSettings.Name, embeddingProviderSettings.Host, embeddingProviderSettings.Hostname, embeddingProviderSettings.Model, HFInferenceProvider.NONE,logger);
    }
    
    private static IProvider CreateProvider(this LLMProviders provider, string instanceName, Host host, string hostname, Model model, HFInferenceProvider inferenceProvider , ILogger logger)
    {
        try
        {
            return provider switch
            {
                LLMProviders.OPEN_AI => new ProviderOpenAI(logger) { InstanceName = instanceName },
                LLMProviders.ANTHROPIC => new ProviderAnthropic(logger) { InstanceName = instanceName },
                LLMProviders.MISTRAL => new ProviderMistral(logger) { InstanceName = instanceName },
                LLMProviders.GOOGLE => new ProviderGoogle(logger) { InstanceName = instanceName },
                LLMProviders.X => new ProviderX(logger) { InstanceName = instanceName },
                LLMProviders.DEEP_SEEK => new ProviderDeepSeek(logger) { InstanceName = instanceName },
                LLMProviders.ALIBABA_CLOUD => new ProviderAlibabaCloud(logger) { InstanceName = instanceName },
                
                LLMProviders.GROQ => new ProviderGroq(logger) { InstanceName = instanceName },
                LLMProviders.FIREWORKS => new ProviderFireworks(logger) { InstanceName = instanceName },
                LLMProviders.HUGGINGFACE => new ProviderHuggingFace(logger, inferenceProvider, model) { InstanceName = instanceName }, 
                
                LLMProviders.SELF_HOSTED => new ProviderSelfHosted(logger, host, hostname) { InstanceName = instanceName },
                
                LLMProviders.HELMHOLTZ => new ProviderHelmholtz(logger) { InstanceName = instanceName },
                LLMProviders.GWDG => new ProviderGWDG(logger) { InstanceName = instanceName },
                
                _ => new NoProvider(),
            };
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to create provider: {e.Message}");
            return new NoProvider();
        }
    }
    
    public static string GetCreationURL(this LLMProviders provider) => provider switch
    {
        LLMProviders.OPEN_AI => "https://platform.openai.com/signup",
        LLMProviders.MISTRAL => "https://console.mistral.ai/",
        LLMProviders.ANTHROPIC => "https://console.anthropic.com/dashboard",
        LLMProviders.GOOGLE => "https://console.cloud.google.com/",
        LLMProviders.X => "https://accounts.x.ai/sign-up",
        LLMProviders.DEEP_SEEK => "https://platform.deepseek.com/sign_up",
        LLMProviders.ALIBABA_CLOUD => "https://account.alibabacloud.com/register/intl_register.htm",
     
        LLMProviders.GROQ => "https://console.groq.com/",
        LLMProviders.FIREWORKS => "https://fireworks.ai/login",
        LLMProviders.HUGGINGFACE => "https://huggingface.co/login",
        
        LLMProviders.HELMHOLTZ => "https://sdlaml.pages.jsc.fz-juelich.de/ai/guides/blablador_api_access/#step-1-register-on-gitlab",
        LLMProviders.GWDG => "https://docs.hpc.gwdg.de/services/saia/index.html#api-request",
        
        _ => string.Empty,
    };

    public static string GetDashboardURL(this LLMProviders provider) => provider switch
    {
        LLMProviders.OPEN_AI => "https://platform.openai.com/usage",
        LLMProviders.MISTRAL => "https://console.mistral.ai/usage/",
        LLMProviders.ANTHROPIC => "https://console.anthropic.com/settings/plans",
        LLMProviders.X => "https://console.x.ai/",
        LLMProviders.GROQ => "https://console.groq.com/settings/usage",
        LLMProviders.GOOGLE => "https://console.cloud.google.com/billing",
        LLMProviders.FIREWORKS => "https://fireworks.ai/account/billing",
        LLMProviders.DEEP_SEEK => "https://platform.deepseek.com/usage",
        LLMProviders.ALIBABA_CLOUD => "https://usercenter2-intl.aliyun.com/billing",
        LLMProviders.HUGGINGFACE => "https://huggingface.co/settings/billing",
        
        _ => string.Empty,
    };

    public static bool HasDashboard(this LLMProviders provider) => provider switch
    {
        LLMProviders.OPEN_AI => true,
        LLMProviders.MISTRAL => true,
        LLMProviders.ANTHROPIC => true,
        LLMProviders.X => true,
        LLMProviders.GROQ => true,
        LLMProviders.FIREWORKS => true,
        LLMProviders.GOOGLE => true,
        LLMProviders.DEEP_SEEK => true,
        LLMProviders.ALIBABA_CLOUD => true,
        LLMProviders.HUGGINGFACE => true,
        
        _ => false,
    };

    public static string GetModelsOverviewURL(this LLMProviders provider, HFInferenceProvider inferenceProvider) => provider switch
    {
        LLMProviders.FIREWORKS => "https://fireworks.ai/models?show=Serverless",
        LLMProviders.HUGGINGFACE => $"https://huggingface.co/models?inference_provider={inferenceProvider.EndpointsId()}",
        _ => string.Empty,
    };

    public static bool IsLLMModelProvidedManually(this LLMProviders provider) => provider switch
    {
        LLMProviders.FIREWORKS => true,
        LLMProviders.HUGGINGFACE => true,
        _ => false,
    };
    
    public static bool IsEmbeddingModelProvidedManually(this LLMProviders provider, Host host) => provider switch
    {
        LLMProviders.SELF_HOSTED => host is not Host.LM_STUDIO,
        _ => false,
    };

    public static bool IsHostNeeded(this LLMProviders provider) => provider switch
    {
        LLMProviders.SELF_HOSTED => true,
        _ => false,
    };

    public static bool IsHostnameNeeded(this LLMProviders provider) => provider switch
    {
        LLMProviders.SELF_HOSTED => true,
        _ => false,
    };

    public static bool IsAPIKeyNeeded(this LLMProviders provider, Host host) => provider switch
    {
        LLMProviders.OPEN_AI => true,
        LLMProviders.MISTRAL => true,
        LLMProviders.ANTHROPIC => true,
        LLMProviders.GOOGLE => true,
        LLMProviders.X => true,
        LLMProviders.DEEP_SEEK => true,
        LLMProviders.ALIBABA_CLOUD => true,
        
        LLMProviders.GROQ => true,
        LLMProviders.FIREWORKS => true,
        LLMProviders.HELMHOLTZ => true,
        LLMProviders.GWDG => true,
        LLMProviders.HUGGINGFACE => true,
        
        LLMProviders.SELF_HOSTED => host is (Host.OLLAMA or Host.VLLM),
        
        _ => false,
    };

    public static bool ShowRegisterButton(this LLMProviders provider) => provider switch
    {
        LLMProviders.OPEN_AI => true,
        LLMProviders.MISTRAL => true,
        LLMProviders.ANTHROPIC => true,
        LLMProviders.GOOGLE => true,
        LLMProviders.X => true,
        LLMProviders.DEEP_SEEK => true,
        LLMProviders.ALIBABA_CLOUD => true,
        
        LLMProviders.GROQ => true,
        LLMProviders.FIREWORKS => true,
        LLMProviders.HELMHOLTZ => true,
        LLMProviders.GWDG => true,
        LLMProviders.HUGGINGFACE => true,
        
        _ => false,
    };

    public static bool CanLoadModels(this LLMProviders provider, Host host, string? apiKey)
    {
        if (provider is LLMProviders.SELF_HOSTED)
        {
            switch (host)
            {
                case Host.NONE:
                case Host.LLAMACPP:
                default:
                    return false;

                case Host.OLLAMA:
                case Host.LM_STUDIO:
                case Host.VLLM:
                    return true;
            }
        }

        if(provider is LLMProviders.NONE)
            return false;
        
        if(string.IsNullOrWhiteSpace(apiKey))
            return false;
        
        return true;
    }
    
    public static bool IsHFInstanceProviderNeeded(this LLMProviders provider) => provider switch
    {
        LLMProviders.HUGGINGFACE => true,
        _ => false,
    };
}