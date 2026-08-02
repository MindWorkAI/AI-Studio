using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Centralizes canonical JSON, structural signatures, and SHA-256 hashes for visual briefings.
/// </summary>
internal static class VisualBriefingHashing
{
    /// <summary>
    /// Computes a lowercase SHA-256 hash for UTF-8 text.
    /// </summary>
    /// <param name="value">The text to hash.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    internal static string Compute(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Computes a hash over unambiguously separated text sections.
    /// </summary>
    /// <param name="values">The ordered text sections.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    internal static string ComputeSections(params string?[] values) => Compute(string.Join('\u001e', values.Select(value => value ?? string.Empty)));

    /// <summary>
    /// Computes a lowercase SHA-256 hash for a file without loading it fully into memory.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The lowercase hexadecimal hash.</returns>
    internal static async Task<string> ComputeFileAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            65_536,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, token));
    }

    /// <summary>
    /// Returns canonical JSON for one value, with ordinally sorted object properties.
    /// </summary>
    /// <remarks>
    /// Hashed values go through here instead of being serialized directly. Plain serialization writes
    /// properties in declaration order, which would tie every stored hash to the order in which the
    /// members happen to appear in the C# file: reordering two properties would invalidate every
    /// briefing already on disk, without any visible change to the data.
    /// </remarks>
    /// <typeparam name="T">The type of the value to canonicalize.</typeparam>
    /// <param name="value">The value to canonicalize.</param>
    /// <returns>Compact canonical JSON.</returns>
    internal static string CanonicalJson<T>(T value) => CanonicalJson(JsonSerializer.SerializeToElement(value, VisualBriefingJson.Canonical));

    /// <summary>
    /// Returns canonical JSON with ordinally sorted object properties.
    /// </summary>
    /// <param name="value">The JSON value to canonicalize.</param>
    /// <returns>Compact canonical JSON.</returns>
    internal static string CanonicalJson(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, value);
        
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Computes the structural signature of canonical business data.
    /// </summary>
    /// <param name="value">The JSON value to inspect.</param>
    /// <returns>A stable hash of its property and collection shape.</returns>
    internal static string StructuralSignature(JsonElement value)
    {
        var builder = new StringBuilder();
        AppendStructuralSignature(builder, value);
        return Compute(builder.ToString());
    }

    /// <summary>
    /// Writes one JSON value in canonical order.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The value to write.</param>
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            
            default:
                value.WriteTo(writer);
                break;
        }
    }

    /// <summary>
    /// Appends type and property shape without business values.
    /// </summary>
    /// <param name="builder">The signature builder.</param>
    /// <param name="value">The value to inspect.</param>
    private static void AppendStructuralSignature(StringBuilder builder, JsonElement value)
    {
        builder.Append(value.ValueKind);
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    builder.Append(property.Name).Append(':');
                    AppendStructuralSignature(builder, property.Value);
                }
                builder.Append('}');
                break;
            
            case JsonValueKind.Array:
                builder.Append('[');
                var first = value.EnumerateArray().FirstOrDefault();
                if (first.ValueKind is not JsonValueKind.Undefined)
                    AppendStructuralSignature(builder, first);
                builder.Append(']');
                break;
        }
    }
}
