namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.SearXNG;

internal sealed record SearXNGSearchRequest(Uri SearchUri, string Query, string? Language, string? TimeRange, int? Page, string? SafeSearch, int EffectiveLimit, int TimeoutSeconds);