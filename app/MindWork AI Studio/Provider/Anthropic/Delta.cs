// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Provider.Anthropic;

/// <summary>
/// The delta object of a response line.
/// </summary>
/// <param name="Type">The type of the delta.</param>
/// <param name="Text">The text of the delta.</param>
public readonly record struct Delta(string Type, string Text);