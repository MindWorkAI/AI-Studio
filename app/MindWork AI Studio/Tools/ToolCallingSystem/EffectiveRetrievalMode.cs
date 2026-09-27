using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// How the data sources of a chat are actually searched, and what is in the way when that is not
/// how the user wants them searched.
/// </summary>
/// <param name="Mode">How the data sources are searched.</param>
/// <param name="FallbackReason">Why AI Studio searches with every message although the user wants semantic search. ToolOfferBlockReason.NONE when the data sources are searched the way the user wants.</param>
public readonly record struct EffectiveRetrievalMode(DataSourceRetrievalMode Mode, ToolOfferBlockReason FallbackReason);