using AIStudio.Provider;

namespace AIStudio.Tests.Models.Corpus;

/// <summary>
/// A corpus entry whose current answer the audit showed to be wrong.
/// </summary>
/// <remarks>
/// While the old rules still stood, these were kept out of the snapshot: it says "this must not
/// change", and writing a known-wrong answer into it would have turned the rebuild into a copy of
/// the mistake. That has been over since the old rules were deleted, and keeping them out had a
/// price nobody had counted -- an entry here states capabilities and nothing else, so the kind, the
/// context window, the image limit and the tokenizer of these models were reviewed nowhere at all.
/// Five embedding models sat in that blind spot. They are in the snapshot now like everything else,
/// and what this file still does is the part no snapshot can: saying what the answer has to be,
/// rather than only noticing that it changed.
/// </remarks>
/// <param name="Provider">The provider the model is reached through.</param>
/// <param name="ModelId">The model ID, exactly as it appears in the corpus.</param>
/// <param name="AnswerToday">What the rules being replaced answered. History now: the code that produced it is gone, so nothing checks this any more. It stays because an entry saying only what is right leaves the reader wondering what was wrong.</param>
/// <param name="AnswerWanted">What the rebuilt rules have to answer.</param>
/// <param name="Reason">Why the current answer is wrong, in one sentence.</param>
/// <param name="Source">Where that can be checked.</param>
public sealed record ExpectedChange(LLMProviders Provider, string ModelId, IReadOnlyList<Capability> AnswerToday, IReadOnlyList<Capability> AnswerWanted, string Reason, string Source);