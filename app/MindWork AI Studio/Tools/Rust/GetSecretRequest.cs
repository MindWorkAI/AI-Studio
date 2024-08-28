using System.Text.Json.Serialization;

namespace AIStudio.Tools.Rust;

public readonly record struct GetSecretRequest(
    string Destination,
    [property:JsonPropertyName("user_name")] string UserName);