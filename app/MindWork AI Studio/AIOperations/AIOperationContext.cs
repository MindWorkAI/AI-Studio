namespace AIStudio.AIOperations;

/// <summary>
/// What a call of an AI operation runs with, apart from its request.
/// </summary>
/// <param name="Provider">The provider which the caller chose for this call, usually through AIOperationProviderResolver.</param>
public sealed record AIOperationContext(Settings.Provider Provider);
