namespace AIStudio.Tools.ToolCallingSystem;

/// <summary>
/// What keeps a tool from being offered to a model, if anything.
/// </summary>
/// <remarks>
/// A reason rather than a yes or no, because whoever asks has to say something different for each
/// of them: a model which cannot use tools is a matter of the provider settings, a tool switched off
/// by the organization is nothing the user can change, and missing settings are something they can
/// fill in themselves.
/// </remarks>
public enum ToolOfferBlockReason
{
    /// <summary>
    /// Nothing is in the way, the tool can be offered.
    /// </summary>
    NONE,

    /// <summary>
    /// The organization switched tools off altogether.
    /// </summary>
    TOOLS_SWITCHED_OFF,

    /// <summary>
    /// The selected model or its provider cannot use tools, or no provider is selected at all.
    /// </summary>
    MODEL_CANNOT_USE_TOOLS,

    /// <summary>
    /// This installation does not know the tool, the tool belongs to a preview which is switched
    /// off, or it is not meant for this part of the app.
    /// </summary>
    NOT_AVAILABLE_HERE,

    /// <summary>
    /// The organization switched this tool off.
    /// </summary>
    TOOL_SWITCHED_OFF,

    /// <summary>
    /// The user switched this tool off in the tool settings, because they never need it.
    /// </summary>
    TOOL_SWITCHED_OFF_BY_USER,

    /// <summary>
    /// A setting the tool cannot work without is missing or invalid.
    /// </summary>
    NOT_CONFIGURED,

    /// <summary>
    /// The provider is not trusted enough for this tool.
    /// </summary>
    PROVIDER_CONFIDENCE_TOO_LOW,

    /// <summary>
    /// The chat holds internal content, such as mails or passages of a data source, and the tool
    /// would send data further than their source allows. A new chat can use the tool again.
    /// </summary>
    OUTBOUND_DATA_RESTRICTED,
}