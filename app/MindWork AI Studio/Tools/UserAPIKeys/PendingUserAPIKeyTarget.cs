using AIStudio.Provider;

namespace AIStudio.Tools.UserAPIKeys;

/// <summary>
/// One provider of a configuration plugin which waits for the API key of the user.
/// </summary>
/// <param name="Name">The name of the provider as the user knows it.</param>
/// <param name="SecretId">Where the API key of the provider is stored.</param>
/// <param name="StoreType">The secret store of the provider's kind, which also tells which models the provider offers.</param>
/// <param name="CreateProvider">Creates the provider, e.g., to check a key. Called only on demand, since every provider brings an HTTP client of its own.</param>
public sealed record PendingUserAPIKeyTarget(string Name, ISecretId SecretId, SecretStoreType StoreType, Func<IProvider> CreateProvider);