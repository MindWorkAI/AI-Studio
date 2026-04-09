using Lua;

namespace AIStudio.Settings.DataModel;

public sealed record DataMandatoryInfo
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger<DataMandatoryInfo>();

    /// <summary>
    /// The stable ID of the mandatory info.
    /// </summary>
    public string Id { get; private init; } = string.Empty;

    /// <summary>
    /// The ID of the enterprise configuration plugin that provides this info.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; private init; } = Guid.Empty;

    /// <summary>
    /// The title shown to the user.
    /// </summary>
    public string Title { get; private init; } = string.Empty;

    /// <summary>
    /// The configured version string. When it changes, the user must accept the text again.
    /// </summary>
    public string VersionText { get; private init; } = string.Empty;

    /// <summary>
    /// The Markdown content shown to the user.
    /// </summary>
    public string Markdown { get; private init; } = string.Empty;

    /// <summary>
    /// The label of the acceptance button.
    /// </summary>
    public string AcceptButtonText { get; private init; } = string.Empty;

    /// <summary>
    /// The label of the reject button.
    /// </summary>
    public string RejectButtonText { get; private init; } = string.Empty;

    public static bool TryParseConfiguration(int idx, LuaTable table, Guid configPluginId, out DataMandatoryInfo mandatoryInfo)
    {
        mandatoryInfo = new DataMandatoryInfo();
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOG.LogWarning("The configured mandatory info {InfoIndex} does not contain a valid ID. The ID must be a valid GUID.", idx);
            return false;
        }

        if (!table.TryGetValue("Title", out var titleValue) || !titleValue.TryRead<string>(out var title) || string.IsNullOrWhiteSpace(title))
        {
            LOG.LogWarning("The configured mandatory info {InfoIndex} does not contain a valid Title field.", idx);
            return false;
        }

        if (!table.TryGetValue("Version", out var versionValue) || !versionValue.TryRead<string>(out var versionText) || string.IsNullOrWhiteSpace(versionText))
        {
            LOG.LogWarning("The configured mandatory info {InfoIndex} does not contain a valid Version field.", idx);
            return false;
        }

        if (!table.TryGetValue("Markdown", out var markdownValue) || !markdownValue.TryRead<string>(out var markdown) || string.IsNullOrWhiteSpace(markdown))
        {
            LOG.LogWarning("The configured mandatory info {InfoIndex} does not contain a valid Markdown field.", idx);
            return false;
        }

        if (!table.TryGetValue("AcceptButtonText", out var acceptButtonValue) || !acceptButtonValue.TryRead<string>(out var acceptButtonText) || string.IsNullOrWhiteSpace(acceptButtonText))
        {
            LOG.LogWarning("The configured mandatory info {InfoIndex} does not contain a valid AcceptButtonText field.", idx);
            return false;
        }

        if (!table.TryGetValue("RejectButtonText", out var rejectButtonValue) || !rejectButtonValue.TryRead<string>(out var rejectButtonText) || string.IsNullOrWhiteSpace(rejectButtonText))
        {
            LOG.LogWarning("The configured mandatory info {InfoIndex} does not contain a valid RejectButtonText field.", idx);
            return false;
        }

        var normalizedMarkdown = AIStudio.Tools.Markdown.RemoveSharedIndentation(markdown);
        mandatoryInfo = new DataMandatoryInfo
        {
            Id = id.ToString(),
            Title = title,
            VersionText = versionText,
            Markdown = normalizedMarkdown,
            AcceptButtonText = acceptButtonText,
            RejectButtonText = rejectButtonText,
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return true;
    }
}