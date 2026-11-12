namespace AIStudio.Tools.Rust;

/// <summary>Request body used to check a tokenizer file in the Rust runtime.</summary>
/// <param name="FilePath">Absolute path of the tokenizer file.</param>
public sealed record ValidateTokenizerRequest(string FilePath);