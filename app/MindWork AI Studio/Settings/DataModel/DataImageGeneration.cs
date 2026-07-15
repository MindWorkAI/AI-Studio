using AIStudio.Provider;

namespace AIStudio.Settings.DataModel;

public sealed class DataImageGeneration
{
    /// <summary>
    /// Do we want to preselect any image generation options?
    /// </summary>
    public bool PreselectOptions { get; set; }

    /// <summary>
    /// The minimum confidence level required for a provider to be considered for refining the image prompt.
    /// </summary>
    public ConfidenceLevel MinimumProviderConfidence { get; set; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The preselected provider used to refine the user's image description into a detailed prompt.
    /// </summary>
    public string PreselectedProvider { get; set; } = string.Empty;

    /// <summary>
    /// The base URL of the local MCP server that offers the image generation tool.
    /// </summary>
    public string MCPServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// An optional bearer token used to authenticate against the MCP server.
    /// </summary>
    public string MCPServerBearerToken { get; set; } = string.Empty;

    /// <summary>
    /// The name of the tool, on the configured MCP server, that generates images.
    /// </summary>
    public string MCPServerToolName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the configured MCP server is trustworthy enough to receive prompts, e.g., because it is
    /// self-hosted or runs within a trusted network. Mirrors the same trust model used for ERI data sources.
    /// </summary>
    public DataSourceSecurity MCPServerSecurityPolicy { get; set; } = DataSourceSecurity.NOT_SPECIFIED;
}
