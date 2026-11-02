using System.Text.Json;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// A request in the fixtures, together with how AI Studio builds it and the options it writes it with.
/// </summary>
/// <param name="FileName">The name of the file in the folder Fixtures next to the tests, which holds the request as it got sent before.</param>
/// <param name="Create">Builds the request the way AI Studio does.</param>
/// <param name="Options">The options AI Studio writes the request with.</param>
public sealed record JsonGoldenRequest(string FileName, Func<object> Create, JsonSerializerOptions Options)
{
    /// <inheritdoc />
    public override string ToString() => this.FileName;
}