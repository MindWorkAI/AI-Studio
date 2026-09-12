using AIStudio.Provider;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// One model name together with what the app has to make of it.
/// </summary>
/// <param name="Provider">The provider the model is reached through.</param>
/// <param name="ModelId">The model ID exactly as that provider reports it, before any normalization.</param>
/// <param name="Kind">What the model is made for.</param>
/// <param name="AnsweredTodayAs">What the markers being replaced answer, where that is something else.</param>
/// <param name="Reason">Why the two differ, which is only filled in when they do.</param>
public sealed record ModelKindExample(LLMProviders Provider, string ModelId, ModelKind Kind, ModelKind? AnsweredTodayAs = null, string Reason = "");