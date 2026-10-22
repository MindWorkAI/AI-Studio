using AIStudio.Provider;
using AIStudio.Provider.SelfHosted;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Chat;

public static class IImageSourceExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(IImageSourceExtensions).Namespace, nameof(IImageSourceExtensions));

    /// <summary>
    /// The largest image, in bytes, we send anywhere: no image prepared for a model is larger, and
    /// TryAsBase64 skips every larger file.
    /// </summary>
    public const long MAX_IMAGE_SIZE_BYTES = 10_000_000;

    /// <summary>
    /// The largest image file, in bytes, which we send through the given provider.
    /// </summary>
    /// <remarks>
    /// The smallest of the limits we know: the one of a self-hosted engine, the one of the provider,
    /// and MAX_IMAGE_SIZE_BYTES, our own. A provider or engine which states no limit gets ours.
    /// </remarks>
    /// <param name="provider">The configured provider the image is sent through.</param>
    /// <returns>The size limit of one image file.</returns>
    public static long MaxImageSizeBytesFor(AIStudio.Settings.Provider provider)
    {
        var engineLimit = provider.UsedLLMProvider is LLMProviders.SELF_HOSTED ? provider.Host.GetMaxImageSizeBytes() : null;
        var providerLimit = provider.UsedLLMProvider.GetMaxImageSizeBytes();
        return Math.Min(Math.Min(engineLimit ?? MAX_IMAGE_SIZE_BYTES, providerLimit ?? MAX_IMAGE_SIZE_BYTES), MAX_IMAGE_SIZE_BYTES);
    }

    /// <summary>
    /// Read the image content as a base64 string.
    /// </summary>
    /// <remarks>
    /// This is for the images inside the content of a chat, which go to external data sources and
    /// agents as they are. Attached images, which go to a model, take ModelImageService instead: it
    /// scales them down and encodes them anew within the limit of the provider.<br/>
    /// <br/>
    /// The images are directly converted to base64 strings. The maximum
    /// size of the image is around 10 MB. If the image is larger, the method
    /// returns an empty string.<br/>
    /// <br/>
    /// Note as well that this method returns just the base64 string without
    /// any data URI prefix (like "data:image/png;base64,"). The caller has
    /// to take care of that if needed.
    /// </remarks>
    /// <param name="image">The image source.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The image content as a base64 string; might be empty.</returns>
    public static async Task<(bool success, string base64Content)> TryAsBase64(this IImageSource image, CancellationToken token = default)
    {
        switch (image.SourceType)
        {
            case ContentImageSource.BASE64:
                return (success: true, image.Source);
            
            case ContentImageSource.URL:
            {
                using var httpClient = ExternalHttpClientTimeout.CreateHttpClient(ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
                using var timeoutTokenSource = ExternalHttpClientTimeout.CreateTimeoutTokenSource(token);
                var timeoutToken = timeoutTokenSource.Token;
                using var response = await httpClient.GetAsync(image.Source, HttpCompletionOption.ResponseHeadersRead, timeoutToken);
                if(response.IsSuccessStatusCode)
                {
                    // Read the length of the content:
                    var lengthBytes = response.Content.Headers.ContentLength;
                    if(lengthBytes > MAX_IMAGE_SIZE_BYTES)
                    {
                        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ImageNotSupported, TB("The image at the URL is too large (>10 MB). Skipping the image.")));
                        return (success: false, string.Empty);
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(timeoutToken);
                    return (success: true, Convert.ToBase64String(bytes));
                }

                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ImageNotSupported, TB("Failed to download the image from the URL. Skipping the image.")));
                return (success: false, string.Empty);
            }

            case ContentImageSource.LOCAL_PATH:
                if(File.Exists(image.Source))
                {
                    // Read the content length:
                    var length = new FileInfo(image.Source).Length;
                    if(length > MAX_IMAGE_SIZE_BYTES)
                    {
                        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ImageNotSupported, TB("The local image file is too large (>10 MB). Skipping the image.")));
                        return (success: false, string.Empty);
                    }

                    var bytes = await File.ReadAllBytesAsync(image.Source, token);
                    return (success: true, Convert.ToBase64String(bytes));
                }

                await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ImageNotSupported, TB("The local image file does not exist. Skipping the image.")));
                return (success: false, string.Empty);
            
            default:
                return (success: false, string.Empty);
        }
    }
}