using System.Text.Json;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// A stored file in the fixtures, together with the type and the options AI Studio reads it with.
/// </summary>
/// <param name="FileName">The name of the file in the folder Fixtures next to the tests.</param>
/// <param name="Type">The type AI Studio reads the file as.</param>
/// <param name="Options">The options AI Studio reads and writes the file with.</param>
public sealed record JsonGoldenFile(string FileName, Type Type, JsonSerializerOptions Options)
{
    /// <inheritdoc />
    public override string ToString() => this.FileName;
}