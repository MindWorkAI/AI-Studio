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
    /// returns an empty string.
    /// 
    /// As of now, this method does no sort of image processing. LLMs usually
    /// do not work with arbitrary image sizes. In the future, we might have
    /// to resize the images before sending them to the model.
    /// </remarks>
    /// <param name="image">The image source.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The image content as a base64 string; might be empty.</returns>
    public static async Task<string> AsBase64(this IImageSource image, CancellationToken token = default)
    {
        switch (image.SourceType)
        {
            case ContentImageSource.BASE64:
                return image.Source;
            
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
                        return string.Empty;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(token);
                    return Convert.ToBase64String(bytes);
                }

                return string.Empty;
            }

            case ContentImageSource.LOCAL_PATH:
                if(File.Exists(image.Source))
                {
                    // Read the content length:
                    var length = new FileInfo(image.Source).Length;
                    if(length > 10_000_000)
                    {
                        await MessageBus.INSTANCE.SendError(new(Icons.Material.Filled.ImageNotSupported, TB("The local image file is too large (>10 MB). Skipping the image.")));
                        return string.Empty;
                    }

                    var bytes = await File.ReadAllBytesAsync(image.Source, token);
                    return Convert.ToBase64String(bytes);
                }

                return string.Empty;
            
            default:
                return string.Empty;
        }
    }
}