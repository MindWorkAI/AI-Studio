namespace AIStudio.Tools.Rust;

/// <summary>Request body used to count the tokens of a text in the Rust runtime.</summary>
/// <param name="Text">The text to count.</param>
/// <param name="TokenizerPath">Path of the tokenizer file to count with.</param>
public sealed record CountTokensRequest(string Text, string TokenizerPath);