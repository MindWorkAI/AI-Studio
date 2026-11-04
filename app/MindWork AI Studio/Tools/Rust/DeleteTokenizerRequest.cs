namespace AIStudio.Tools.Rust;

/// <summary>Request body used to delete the stored tokenizer of a model in the Rust runtime.</summary>
/// <param name="ModelId">The model whose tokenizer gets deleted.</param>
public sealed record DeleteTokenizerRequest(string ModelId);