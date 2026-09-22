using System.Text.Json;

namespace AIStudio.Provider.Anthropic;

public sealed record AnthropicToolUse
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public JsonElement Input { get; init; }

    /// <summary>
    /// The arguments as they came off the wire, set only when they never parsed into an object.
    /// </summary>
    /// <remarks>
    /// Only a streamed round can have these: the arguments arrive in fragments there, and a
    /// stream which ends mid-fragment leaves text which is not an object. The block carries an
    /// empty object in that case, because that is what may go back to the provider -- while the
    /// call itself has to be rejected rather than run without the arguments it asked for.
    /// </remarks>
    public string? UnparsableArguments { get; init; }

    /// <summary>
    /// The arguments as JSON text, which is what the tool executor works with.
    /// </summary>
    public string Arguments => this.UnparsableArguments ?? (this.Input.ValueKind is JsonValueKind.Undefined ? "{}" : this.Input.GetRawText());
}