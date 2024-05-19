namespace AIStudio.Settings;

/// <summary>
/// The data model for the settings file.
/// </summary>
public sealed class Data
{
    /// <summary>
    /// The version of the settings file. Allows us to upgrade the settings,
    /// when a new version is available.
    /// </summary>
    public Version Version { get; init; } = Version.V1;

    /// <summary>
    /// List of configured providers.
    /// </summary>
    public List<Provider> Providers { get; init; } = [];

    /// <summary>
    /// The next provider number to use.
    /// </summary>
    public uint NextProviderNum { get; set; } = 1;

    /// <summary>
    /// Should we save energy? When true, we will update content streamed
    /// from the server, i.e., AI, less frequently.
    /// </summary>
    public bool IsSavingEnergy { get; set; }

    /// <summary>
    /// Shortcuts to send the input to the AI.
    /// </summary>
    public SendBehavior ShortcutSendBehavior { get; set; } = SendBehavior.MODIFER_ENTER_IS_SENDING;
}