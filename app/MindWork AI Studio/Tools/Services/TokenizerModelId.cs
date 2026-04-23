namespace AIStudio.Tools.Services;

public static class TokenizerModelId
{
    public static string ForProvider(Settings.Provider provider) => ForProviderId(provider.Id);

    public static string ForProviderId(string guid) => "chat_" + NormalizeGuid(guid);

    public static string ForEmbeddingProvider(Settings.EmbeddingProvider provider) => ForEmbeddingProviderId(provider.Id);

    public static string ForEmbeddingProviderId(string guid) => "embedding_" + NormalizeGuid(guid);

    private static string NormalizeGuid(string guid)
    {
        if (Guid.TryParse(guid, out var parsedGuid))
            return parsedGuid.ToString("D");

        return guid.Trim();
    }
}
