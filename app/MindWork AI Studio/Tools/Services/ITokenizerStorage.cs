using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

/// <summary>
/// Checks, stores, and deletes the tokenizers the app keeps below its data directory.
/// </summary>
/// <remarks>
/// The runtime does the work, and RustService passes the calls on. Code which decides when a
/// tokenizer has to be checked or stored takes this interface instead of the service, so tests can
/// examine these decisions without a running runtime.
/// </remarks>
internal interface ITokenizerStorage
{
    /// <summary>
    /// Checks whether a file is a tokenizer the runtime can load.
    /// </summary>
    /// <param name="filePath">The tokenizer file to check.</param>
    /// <returns>Whether the file is a usable tokenizer, and if not, why.</returns>
    public Task<TokenizerResponse> ValidateTokenizer(string filePath);

    /// <summary>
    /// Copies a tokenizer below the data directory, replacing what was stored for the model before.
    /// </summary>
    /// <param name="modelId">The model the tokenizer belongs to, as TokenizerModelId builds it.</param>
    /// <param name="filePath">The tokenizer file to copy.</param>
    /// <returns>Whether storing succeeded, along with the path of the stored copy.</returns>
    public Task<TokenizerResponse> StoreTokenizer(string modelId, string filePath);

    /// <summary>
    /// Deletes what is stored for the model. Nothing stored counts as success.
    /// </summary>
    /// <param name="modelId">The model the tokenizer belongs to, as TokenizerModelId builds it.</param>
    /// <returns>Whether deleting succeeded.</returns>
    public Task<TokenizerResponse> DeleteTokenizer(string modelId);
}