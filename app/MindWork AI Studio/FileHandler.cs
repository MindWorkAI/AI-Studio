using Microsoft.AspNetCore.StaticFiles;

namespace AIStudio;

internal static class FileHandler
{
    private const string ENDPOINT = "/local/file";
    
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(FileHandler));

    internal static string CreateFileUrl(string filePath)
    {
        var encodedPath = Uri.EscapeDataString(filePath);
        return $"{ENDPOINT}?path={encodedPath}";
    }
    
    internal static async Task HandlerAsync(HttpContext context, Func<Task> nextHandler)
    {
        var requestPath = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(requestPath) || !requestPath.Equals(ENDPOINT, StringComparison.Ordinal))
        {
            await nextHandler();
            return;
        }

        // Extract the file path from the query parameter:
        // Format: /local/file?path={url-encoded-path}
        if (!context.Request.Query.TryGetValue("path", out var pathValues) || pathValues.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            LOGGER.LogWarning("No file path provided in the request. Using ?path={{url-encoded-path}} format.");
            return;
        }

        // The query parameter is automatically URL-decoded by ASP.NET Core:
        var filePath = pathValues[0];
        if (string.IsNullOrWhiteSpace(filePath))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            LOGGER.LogWarning("Empty file path provided in the request.");
            return;
        }

        // Security check: Prevent path traversal attacks:
        var fullPath = Path.GetFullPath(filePath);
        if (fullPath != filePath && !filePath.StartsWith('/'))
        {
            // On Windows, absolute paths may differ, so we do an additional check
            // to ensure no path traversal sequences are present:
            if (filePath.Contains(".."))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                LOGGER.LogWarning("Path traversal attempt detected: {FilePath}", filePath);
                return;
            }
        }

        // Check if the file exists:
        if (!File.Exists(filePath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            LOGGER.LogWarning("Requested file not found: '{FilePath}'", filePath);
            return;
        }

        // Determine the content type:
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        if (!contentTypeProvider.TryGetContentType(filePath, out var contentType))
            contentType = "application/octet-stream";

        // Set response headers:
        context.Response.ContentType = contentType;
        context.Response.Headers.ContentDisposition = $"inline; filename=\"{Path.GetFileName(filePath)}\"";

        // Stream the file to the response:
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);
        context.Response.ContentLength = fileStream.Length;
        await fileStream.CopyToAsync(context.Response.Body);
    }
}
