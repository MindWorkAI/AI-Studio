using System.Text.Json.Serialization;

namespace AIStudio.Tools.Rust;

/// <param name="Texts">The contents to filter. The runtime answers with one result per entry, in this order.</param>
public readonly record struct SanitizePromptInjectionsBatchRequest([property: JsonPropertyName("texts")] IReadOnlyList<string> Texts);