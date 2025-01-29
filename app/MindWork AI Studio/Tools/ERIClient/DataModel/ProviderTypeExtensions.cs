namespace AIStudio.Tools.ERIClient.DataModel;

public static class ProviderTypeExtensions
{
    public static string Explain(this ProviderType providerType) => providerType switch
    {
        ProviderType.NONE => "The related data is not allowed to be sent to any LLM provider. This means that this data source cannot be used at the moment.",
        ProviderType.ANY => "The related data can be sent to any provider, regardless of where it is hosted (cloud or self-hosted).",
        ProviderType.SELF_HOSTED => "The related data can be sent to a provider that is hosted by the same organization, either on-premises or locally. Cloud-based providers are not allowed.",
        
        _ => "Unknown configuration. This data source cannot be used at the moment.",
    };
}