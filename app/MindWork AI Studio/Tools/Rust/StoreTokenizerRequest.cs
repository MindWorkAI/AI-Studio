namespace AIStudio.Tools.Rust;

/// <summary>Request body used to store a copy of a tokenizer file for a model in the Rust runtime.</summary>
/// <param name="ModelId">The model the tokenizer belongs to.</param>
/// <param name="FilePath">Absolute path of the tokenizer file.</param>
public sealed record StoreTokenizerRequest(string ModelId, string FilePath);