using AIStudio.Provider;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// A corpus entry whose current answer the audit showed to be wrong.
/// </summary>
/// <remarks>
/// These are kept apart from the snapshot on purpose. The snapshot says "this must not change", and
/// writing a known-wrong answer into it would turn the rebuild into a copy of the mistake. Here the
/// wrong answer is written down as well, so that it cannot drift unnoticed either, next to the
/// answer the rebuild has to arrive at.
/// </remarks>
/// <param name="Provider">The provider the model is reached through.</param>
/// <param name="ModelId">The model ID, exactly as it appears in the corpus.</param>
/// <param name="AnswerToday">What the current rules answer. Written down so a change here is seen.</param>
/// <param name="AnswerWanted">What the rebuilt rules have to answer.</param>
/// <param name="Reason">Why the current answer is wrong, in one sentence.</param>
/// <param name="Source">Where that can be checked.</param>
public sealed record ExpectedChange(LLMProviders Provider, string ModelId, IReadOnlyList<Capability> AnswerToday, IReadOnlyList<Capability> AnswerWanted, string Reason, string Source);