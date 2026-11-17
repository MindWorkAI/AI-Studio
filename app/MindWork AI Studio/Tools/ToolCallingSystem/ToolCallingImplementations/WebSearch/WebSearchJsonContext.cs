using System.Text.Json.Serialization;

using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Tavily;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// The contracts with which System.Text.Json reads and writes the calls to the search services, built at
/// compile time.
/// </summary>
/// <remarks>
/// SearXNG is missing on purpose: its answers differ between instances, so its client reads them as a tree
/// of JSON nodes instead.
/// </remarks>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(StaanSearchRequest))]
[JsonSerializable(typeof(StaanSearchResponse))]
[JsonSerializable(typeof(TavilySearchRequest))]
[JsonSerializable(typeof(TavilySearchResponse))]
internal sealed partial class WebSearchJsonContext : JsonSerializerContext;