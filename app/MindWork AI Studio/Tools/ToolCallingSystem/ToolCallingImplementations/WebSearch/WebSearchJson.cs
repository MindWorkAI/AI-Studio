using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// How the search backends read and write the JSON of their APIs.
/// </summary>
/// <remarks>
/// Search APIs name their fields in snake case, so one naming policy here spares nearly every
/// field of every DTO a property name attribute. What is left of null is not written, which is
/// how a request leaves out a parameter instead of sending it empty: a search service usually
/// treats an empty parameter as a value rather than as an omission.
/// </remarks>
internal static class WebSearchJson
{
    public static readonly JsonSerializerOptions OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}