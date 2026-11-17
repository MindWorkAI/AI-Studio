using System.Text.Json;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// A type which AI Studio reads or writes as JSON on its own, together with the options it uses for that.
/// </summary>
/// <param name="Type">The type which gets serialized. Whatever it holds gets checked along with it.</param>
/// <param name="Options">The options AI Studio passes to System.Text.Json for this type.</param>
public sealed record JsonTestRoot(Type Type, JsonSerializerOptions Options)
{
    /// <inheritdoc />
    public override string ToString() => JsonContractDescription.TypeName(this.Type);
}