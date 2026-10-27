using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Settings.DataModel;

/// <summary>
/// What a configuration plugin tells the user while AI Studio asks for the API key of the providers
/// which that plugin marked with "PromptForUserAPIKey".
/// </summary>
public sealed record DataUserAPIKeyPrompt : ILivePluginContent
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger<DataUserAPIKeyPrompt>();

    /// <summary>
    /// The stable ID of the prompt. A configuration plugin has at most one, so this is the plugin's ID.
    /// </summary>
    public string Id { get; private init; } = string.Empty;

    /// <summary>
    /// The ID of the enterprise configuration plugin that provides this prompt.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; private init; } = Guid.Empty;

    /// <summary>
    /// The optional title shown to the user. When empty, AI Studio shows a title of its own.
    /// </summary>
    public string Title { get; private init; } = string.Empty;

    /// <summary>
    /// The Markdown instructions which explain how to obtain the API key.
    /// </summary>
    public string Markdown { get; private init; } = string.Empty;

    public static bool TryParseConfiguration(LuaTable table, Guid configPluginId, out DataUserAPIKeyPrompt prompt)
    {
        prompt = new DataUserAPIKeyPrompt();
        if (!table.TryGetValue("Markdown", out var markdownValue) || !markdownValue.TryRead<string>(out var markdown) || string.IsNullOrWhiteSpace(markdown))
        {
            LOG.LogWarning("The configured user API key prompt does not contain a valid Markdown field (config plugin id: {ConfigPluginId}).", configPluginId);
            return false;
        }

        var title = string.Empty;
        if (table.TryGetValue("Title", out var titleValue))
        {
            if (titleValue.TryRead<string>(out var titleText))
                title = titleText;
            else
                LOG.LogWarning("The configured user API key prompt does not contain a valid Title field. Falling back to the built-in title (config plugin id: {ConfigPluginId}).", configPluginId);
        }

        prompt = new DataUserAPIKeyPrompt
        {
            Id = configPluginId.ToString(),
            Title = title.Trim(),
            Markdown = AIStudio.Tools.Markdown.RemoveSharedIndentation(markdown),
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return true;
    }
}
