using System.Text.Json.Serialization;

namespace AIStudio.Tools.Rust;

/// <param name="Results">One result per requested text, in request order. Callers match results to their texts by index.</param>
public readonly record struct SanitizePromptInjectionsBatchResponse([property: JsonPropertyName("results")] IReadOnlyList<SanitizePromptInjectionsResponse> Results);