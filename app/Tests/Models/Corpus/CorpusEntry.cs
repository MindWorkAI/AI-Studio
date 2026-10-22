using AIStudio.Provider;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// One model of the corpus, written the way one provider writes it.
/// </summary>
/// <param name="Provider">The provider the model is reached through.</param>
/// <param name="ModelId">The model ID exactly as that provider reports it, before any normalization.</param>
/// <param name="Origin">Where this spelling comes from.</param>
public sealed record CorpusEntry(LLMProviders Provider, string ModelId, CorpusOrigin Origin);