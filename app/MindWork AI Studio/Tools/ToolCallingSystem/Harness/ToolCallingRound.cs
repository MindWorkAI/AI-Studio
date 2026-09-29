namespace AIStudio.Tools.ToolCallingSystem.Harness;

/// <summary>
/// The outcome of one round of a tool calling conversation, in a shape that no longer depends on
/// the provider API it came from.
/// </summary>
/// <param name="TextOutput">
/// The text the model produced, empty when it only requested tool calls. The loop reads this to
/// tell an answered round from a silent one; it does not show it, because the very same text has
/// already reached the user as deltas while the round was running.
/// </param>
/// <param name="Calls">The tool calls the model requested, empty when it answered instead.</param>
/// <param name="Sources">Sources the provider itself attached, such as those of a provider-native web search.</param>
public sealed record ToolCallingRound(string TextOutput, IReadOnlyList<ToolCallingRequestedCall> Calls, IReadOnlyList<ISource> Sources);