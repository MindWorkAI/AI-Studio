namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// A link a tool offers next to one group of its settings.
/// </summary>
/// <remarks>
/// This is where a group says how to obtain what it asks for: an account to create, a
/// dashboard showing what is left of a quota, the documentation of a setting that cannot be
/// explained in one help text. Without it, a field asking for an API key leaves the user to
/// find out on their own where that key comes from.
/// </remarks>
/// <param name="Label">What the user reads on the button.</param>
/// <param name="Url">Where the button leads. It opens in the browser, not in AI Studio.</param>
/// <param name="Icon">The icon shown before the label.</param>
public sealed record ToolSettingsGroupLink(string Label, string Url, string Icon = Icons.Material.Filled.OpenInBrowser);