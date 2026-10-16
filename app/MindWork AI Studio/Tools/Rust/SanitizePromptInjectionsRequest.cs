using System.Text.Json.Serialization;

namespace AIStudio.Tools.Rust;

/// <param name="Text">The content to filter.</param>
public readonly record struct SanitizePromptInjectionsRequest([property: JsonPropertyName("text")] string Text);