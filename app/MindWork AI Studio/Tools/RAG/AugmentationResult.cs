using AIStudio.Chat;

namespace AIStudio.Tools.RAG;

/// <summary>
/// What an augmentation process did: the chat thread it augmented, and what it handed to the AI.
/// </summary>
/// <remarks>
/// The contexts the AI received can be fewer than the ones retrieved, e.g., when a check of which
/// passages fit the question left some out, or when not all of them fit into what the model can
/// read. Whoever lists the sources and passages of the answer takes them from here, so the user
/// sees what the AI read and nothing else. Whoever restricts the chat for the data sources which
/// brought passages into it counts them here as well, so that a data source none of whose passages
/// reached the AI restricts nothing.
/// </remarks>
/// <param name="ChatThread">The augmented chat thread.</param>
/// <param name="UsedContexts">The retrieval contexts handed to the AI, in the order it received them.</param>
/// <param name="PassageCounts">How many retrieval contexts of each data source the AI received, in the order the data sources were handed in.</param>
public sealed record AugmentationResult(ChatThread ChatThread, IReadOnlyList<PreparedRetrievalContext> UsedContexts, IReadOnlyList<int> PassageCounts);