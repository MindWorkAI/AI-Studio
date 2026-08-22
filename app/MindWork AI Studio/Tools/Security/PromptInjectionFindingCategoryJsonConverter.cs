using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIStudio.Tools.Security;

public sealed class PromptInjectionFindingCategoryJsonConverter() : JsonStringEnumConverter<PromptInjectionFindingCategory>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false);