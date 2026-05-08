using AIStudio.Tools.Databases.Qdrant;

namespace AIStudio.Tools.Databases;

public class EmbeddingStoreFactory
{
    public static EmbeddingStore Create(EmbeddingStoreConfiguration configuration) => configuration.Kind switch
    {
        EmbeddingStoreKind.NONE => new NoEmbeddingStore(configuration.Name, configuration.UnavailableReason ?? "unknown"),
        _ when configuration.Location is null => new NoEmbeddingStore(configuration.Name, $"No location specified for {configuration.Name}"),
        EmbeddingStoreKind.QDRANT_REMOTE when configuration.Location is RemoteLocation location=> new QdrantClientImplementation(configuration.Name, location.Path, location.HttpPort, location.GrpcPort, location.Fingerprint, location.ApiToken),
        _ => throw new ArgumentException("Invalid configuration for " + configuration.Name, nameof(configuration)),
    };
}

public enum EmbeddingStoreKind
{
    NONE,
    QDRANT_EMBED,
    QDRANT_REMOTE,
}

public abstract record EmbeddingStoreLocation;

public sealed record EmbeddedLocation(string Path) : EmbeddingStoreLocation;

public sealed record RemoteLocation(string Path, int? HttpPort, int? GrpcPort, string? Fingerprint, string? ApiToken) : EmbeddingStoreLocation;

public sealed record EmbeddingStoreConfiguration(
    EmbeddingStoreKind Kind,
    string Name,
    EmbeddingStoreLocation? Location,
    string? UnavailableReason);