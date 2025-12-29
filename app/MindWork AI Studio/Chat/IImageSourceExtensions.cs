using AIStudio.Tools.PluginSystem;

namespace AIStudio.Chat;

public static class IImageSourceExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(IImageSourceExtensions).Namespace, nameof(IImageSourceExtensions));
    
    /// <summary>
    /// Read the image content as a base64 string.
    /// </summary>
    /// <remarks>
    /// The images are directly converted to base64 strings. The maximum
    /// size of the image is around 10 MB. If the image is larger, the method
    /// returns an empty string.<br/>
    /// <br/>
    /// As of now, this method does no sort of image processing. LLMs usually
    /// do not work with arbitrary image sizes. In the future, we might have
    /// to resize the images before sending them to the model.<br/>
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
                using var httpClient = new HttpClient();
                using var response = await httpClient.GetAsync(image.Source, HttpCompletionOption.ResponseHeadersRead, token);
                if(response.IsSuccessStatusCode)
                {
                    // Read the length of the content:
                    var lengthBytes = response.Content.Headers.ContentLength;
                    if(lengthBytes > 10_000_000)
                    {
                        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ImageNotSupported, TB("The image at the URL is too large (>10 MB). Skipping the image.")));
                        return (success: false, string.Empty);
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(token);
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
                    if(length > 10_000_000)
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