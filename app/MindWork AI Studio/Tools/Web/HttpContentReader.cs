using System.Text;

namespace AIStudio.Tools.Web;

/// <summary>
/// Reads an HTTP response body as text, without trusting what it claims about its size.
/// </summary>
public static class HttpContentReader
{
    private const int CHUNK_SIZE = 8192;

    /// <summary>
    /// Reads the body as text, refusing anything beyond the given limit.
    /// </summary>
    /// <remarks>
    /// The declared content length is checked first, and the actual bytes are counted while
    /// reading — a server may understate the length or omit it entirely. Counting happens after
    /// decompression, so a small compressed body that expands into a large one is caught too.
    /// </remarks>
    /// <param name="content">The response body.</param>
    /// <param name="maxResponseBytes">The most that may be read.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The body as text, decoded by its declared charset or UTF-8.</returns>
    /// <exception cref="HttpRequestException">The body exceeds the limit.</exception>
    public static async Task<string> ReadAsStringWithLimitAsync(HttpContent content, int maxResponseBytes, CancellationToken token)
    {
        if (content.Headers.ContentLength is { } contentLength && contentLength > maxResponseBytes)
            throw new HttpRequestException($"The response body is too large. Maximum allowed size is {maxResponseBytes} bytes.");

        await using var stream = await content.ReadAsStreamAsync(token);
        await using var buffer = new MemoryStream();
        var chunk = new byte[CHUNK_SIZE];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, token);
            if (read is 0)
                break;

            if (buffer.Length + read > maxResponseBytes)
                throw new HttpRequestException($"The response body is too large. Maximum allowed size is {maxResponseBytes} bytes.");

            buffer.Write(chunk, 0, read);
        }

        return (TryGetContentEncoding(content) ?? Encoding.UTF8).GetString(buffer.ToArray());
    }

    private static Encoding? TryGetContentEncoding(HttpContent content)
    {
        var charset = content.Headers.ContentType?.CharSet?.Trim();
        if (string.IsNullOrWhiteSpace(charset))
            return null;

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch
        {
            // An unknown or malformed charset is not worth failing the request over; the caller
            // falls back to UTF-8, which is what such a server almost always meant.
            return null;
        }
    }
}