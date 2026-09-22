using AIStudio.Provider;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// One model of the corpus which no rule answers for, together with why that is all right.
/// </summary>
/// <param name="Provider">The provider the model is reached through.</param>
/// <param name="ModelId">The model ID exactly as that provider reports it.</param>
/// <param name="Reason">Why this model is left to the global default.</param>
public sealed record ModelLeftToTheDefault(LLMProviders Provider, string ModelId, string Reason);