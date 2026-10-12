using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// How the data sources of a chat are actually searched, and whether Semantic Search could be used
/// there at all.
/// </summary>
/// <param name="Mode">How the data sources are searched.</param>
/// <param name="SemanticSearchBlockReason">Why Semantic Search cannot be offered in this chat, whatever the user prefers. ToolOfferBlockReason.NONE when it can; the chat then searches the way the user wants.</param>
public readonly record struct EffectiveRetrievalMode(DataSourceRetrievalMode Mode, ToolOfferBlockReason SemanticSearchBlockReason);