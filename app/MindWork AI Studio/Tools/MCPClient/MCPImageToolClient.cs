using System.Text;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.MCPClient;

/// <summary>
/// A thin wrapper around the MCP client SDK for talking to a locally configured
/// MCP server that exposes an image-generation tool over Streamable HTTP.
/// </summary>
public static class MCPImageToolClient
{
    // Local image generation can take a lot longer than typical API calls, so we use a
    // generous, dedicated timeout here instead of the app-wide HTTP client timeout.
    private static readonly TimeSpan TOOL_CALL_TIMEOUT = TimeSpan.FromMinutes(10);

    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(MCPImageToolClient));

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(MCPImageToolClient).Namespace, nameof(MCPImageToolClient));

    /// <summary>
    /// Connects to the given MCP server and lists the tools it offers.
    /// </summary>
    public static async Task<MCPResult<List<McpClientTool>>> ListToolsAsync(string serverUrl, string bearerToken, CancellationToken token = default)
    {
        try
        {
            await using var client = await CreateClientAsync(serverUrl, bearerToken, token);
            var tools = await client.ListToolsAsync(cancellationToken: token);
            return new()
            {
                Successful = true,
                Data = [..tools],
            };
        }
        catch (TaskCanceledException e)
        {
            LOGGER.LogWarning(e, "Failed to connect to the MCP server '{ServerUrl}': the request was canceled or timed out.", serverUrl);
            return new()
            {
                Successful = false,
                Message = TB("Failed to connect to the MCP server: the request was canceled either by the user or due to a timeout."),
            };
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Failed to connect to the MCP server '{ServerUrl}'.", serverUrl);
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to connect to the MCP server due to an exception: {0}"), e.Message),
            };
        }
    }

    /// <summary>
    /// Calls the given tool on the configured MCP server with a text prompt and extracts the returned image.
    /// </summary>
    public static async Task<MCPResult<MCPGeneratedImage>> CallImageToolAsync(string serverUrl, string bearerToken, string toolName, string prompt, CancellationToken token = default)
    {
        try
        {
            await using var client = await CreateClientAsync(serverUrl, bearerToken, token);
            var result = await client.CallToolAsync(toolName, new Dictionary<string, object?>
            {
                ["prompt"] = prompt,
            }, cancellationToken: token);

            if (result.IsError == true)
            {
                var errorText = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
                LOGGER.LogWarning("The MCP tool '{ToolName}' on server '{ServerUrl}' reported an error: {ErrorText}", toolName, serverUrl, errorText);
                return new()
                {
                    Successful = false,
                    Message = string.IsNullOrWhiteSpace(errorText) ? TB("The MCP tool reported an error without further details.") : errorText,
                };
            }

            var imageBlock = result.Content.OfType<ImageContentBlock>().FirstOrDefault();
            if (imageBlock is null)
            {
                LOGGER.LogWarning("The MCP tool '{ToolName}' on server '{ServerUrl}' did not return an image content block. Returned content types: {ContentTypes}", toolName, serverUrl, string.Join(", ", result.Content.Select(c => c.GetType().Name)));
                return new()
                {
                    Successful = false,
                    Message = TB("The MCP tool did not return an image."),
                };
            }

            return new()
            {
                Successful = true,
                Data = new MCPGeneratedImage(Encoding.UTF8.GetString(imageBlock.Data.Span), imageBlock.MimeType),
            };
        }
        catch (TaskCanceledException e)
        {
            LOGGER.LogWarning(e, "Failed to call the MCP tool '{ToolName}' on server '{ServerUrl}': the request was canceled or timed out.", toolName, serverUrl);
            return new()
            {
                Successful = false,
                Message = TB("Failed to generate the image: the request was canceled either by the user or due to a timeout."),
            };
        }
        catch (Exception e)
        {
            LOGGER.LogError(e, "Failed to call the MCP tool '{ToolName}' on server '{ServerUrl}'.", toolName, serverUrl);
            return new()
            {
                Successful = false,
                Message = string.Format(TB("Failed to generate the image due to an exception: {0}"), e.Message),
            };
        }
    }

    private static async Task<McpClient> CreateClientAsync(string serverUrl, string bearerToken, CancellationToken token)
    {
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(bearerToken))
            headers["Authorization"] = $"Bearer {bearerToken}";

        var httpClient = ExternalHttpClientTimeout.CreateHttpClient(ExternalHttpTrustPolicy.ALLOW_CUSTOM_ROOTS_WHEN_HOST_WHITELISTED);
        httpClient.Timeout = TOOL_CALL_TIMEOUT;

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(serverUrl),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = headers,
            ConnectionTimeout = TOOL_CALL_TIMEOUT,
        }, httpClient, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: token);
    }
}
