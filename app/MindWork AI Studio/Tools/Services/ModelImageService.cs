using AIStudio.Chat;
using AIStudio.Tools.Rust;

using Microsoft.Extensions.Caching.Memory;
// ReSharper disable NotAccessedPositionalProperty.Local

namespace AIStudio.Tools.Services;

/// <summary>
/// Prepares the images which go to an AI model, and keeps the latest results for a while.
/// </summary>
/// <remarks>
/// Every message builds the whole conversation anew, with every image in it. Without the cache,
/// each photo of a chat would be decoded and encoded again for each message, which a small device
/// such as a Raspberry Pi notices. The cache also lets the Batch Processing assistant check an
/// image ahead of its request without preparing it twice.
///
/// The cache is a MemoryCache of its own: the shared one, which the Markdown library uses, has no
/// size limit, and its entries state no size.
/// </remarks>
/// <param name="rustService">The runtime which prepares the images.</param>
/// <param name="logger">The logger.</param>
public sealed class ModelImageService(RustService rustService, ILogger<ModelImageService> logger) : IDisposable
{
    /// <summary>
    /// How much memory the cached images may take: a 1 MB photo takes about 2.7 MB as base64 in a string.
    /// </summary>
    private const long CACHE_SIZE_LIMIT_BYTES = 32 * 1024 * 1024;

    /// <summary>
    /// How long a cached image waits for its next message. A chat paused for longer prepares its images again.
    /// </summary>
    private static readonly TimeSpan CACHE_SLIDING_EXPIRATION = TimeSpan.FromMinutes(10);

    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = CACHE_SIZE_LIMIT_BYTES });

    /// <summary>
    /// Prepares an attached image for the model of the given provider.
    /// </summary>
    /// <remarks>
    /// A changed file and another byte budget each make for a new preparation. An image which could
    /// not be prepared is not cached, so the next message tries it again, e.g., once the network
    /// drive holding it is back.
    /// </remarks>
    /// <param name="image">The attached image.</param>
    /// <param name="provider">The configured provider the image goes to.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The prepared image, or why it could not be prepared.</returns>
    public async Task<PreparedModelImage> PrepareAsync(FileAttachmentImage image, AIStudio.Settings.Provider provider, CancellationToken token = default)
    {
        var file = new FileInfo(image.FilePath);
        if (!file.Exists)
        {
            logger.LogWarning("The image '{ImagePath}' does not exist anymore and cannot go to the model.", image.FilePath);
            return PreparedModelImage.Failed(ModelImageErrorCode.FILE_NOT_FOUND);
        }

        var maxBytes = IImageSourceExtensions.MaxImageSizeBytesFor(provider);
        var key = new CacheKey(file.FullName, file.LastWriteTimeUtc, file.Length, maxBytes);
        if (this.cache.TryGetValue(key, out PreparedModelImage? cached) && cached is not null)
        {
            logger.LogInformation("Took the image '{ImagePath}' for a budget of {MaxBytes} bytes from the cache.", file.FullName, maxBytes);
            return cached;
        }

        var prepared = await rustService.PrepareImageForModelAsync(file.FullName, maxBytes, token);
        if (!prepared.IsPrepared)
        {
            logger.LogWarning("The image '{ImagePath}' could not be prepared for the model: {ErrorCode}.", file.FullName, prepared.ErrorCode);
            return prepared;
        }

        //
        // The runtime logs the sizes before and after, so this line only says where it came from:
        //
        logger.LogInformation("The runtime prepared the image '{ImagePath}' for a budget of {MaxBytes} bytes; it is cached now.", file.FullName, maxBytes);
        this.cache.Set(key, prepared, new MemoryCacheEntryOptions
        {
            Size = prepared.Base64.Length * (long)sizeof(char),
            SlidingExpiration = CACHE_SLIDING_EXPIRATION,
        });

        return prepared;
    }

    /// <inheritdoc />
    public void Dispose() => this.cache.Dispose();

    /// <summary>
    /// What makes a prepared image valid: the file as it is now, and the budget it was made for.
    /// </summary>
    private readonly record struct CacheKey(string FullPath, DateTime LastWriteTimeUtc, long Length, long MaxBytes);
}