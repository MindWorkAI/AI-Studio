using AIStudio.Tools.PluginSystem;

using Lua;

namespace AIStudio.Settings.DataModel;

public sealed record DataIntroduction : ILivePluginContent
{
    private static readonly ILogger LOG = Program.LOGGER_FACTORY.CreateLogger<DataIntroduction>();

    /// <summary>
    /// The stable ID of the introduction.
    /// </summary>
    public string Id { get; private init; } = string.Empty;

    /// <summary>
    /// The ID of the enterprise configuration plugin that provides this introduction.
    /// </summary>
    public Guid EnterpriseConfigurationPluginId { get; private init; } = Guid.Empty;

    /// <summary>
    /// The title shown to the user.
    /// </summary>
    public string Title { get; private init; } = string.Empty;

    /// <summary>
    /// The configured version string shown to the user.
    /// </summary>
    public string VersionText { get; private init; } = string.Empty;

    /// <summary>
    /// The sort index used on the home page.
    /// </summary>
    public int Index { get; private init; } = 1;

    /// <summary>
    /// The Markdown content shown to the user.
    /// </summary>
    public string Markdown { get; private init; } = string.Empty;

    public static bool TryParseConfiguration(int idx, LuaTable table, Guid configPluginId, out DataIntroduction introduction)
    {
        introduction = new DataIntroduction();
        if (!table.TryGetValue("Id", out var idValue) || !idValue.TryRead<string>(out var idText) || !Guid.TryParse(idText, out var id))
        {
            LOG.LogWarning("The configured introduction {IntroductionIndex} does not contain a valid ID. The ID must be a valid GUID.", idx);
            return false;
        }

        if (!table.TryGetValue("Title", out var titleValue) || !titleValue.TryRead<string>(out var title) || string.IsNullOrWhiteSpace(title))
        {
            LOG.LogWarning("The configured introduction {IntroductionIndex} does not contain a valid Title field.", idx);
            return false;
        }

        if (!table.TryGetValue("Version", out var versionValue) || !versionValue.TryRead<string>(out var versionText) || string.IsNullOrWhiteSpace(versionText))
        {
            LOG.LogWarning("The configured introduction {IntroductionIndex} does not contain a valid Version field.", idx);
            return false;
        }

        if (!table.TryGetValue("Markdown", out var markdownValue) || !markdownValue.TryRead<string>(out var markdown) || string.IsNullOrWhiteSpace(markdown))
        {
            LOG.LogWarning("The configured introduction {IntroductionIndex} does not contain a valid Markdown field.", idx);
            return false;
        }

        var index = 1;
        if (table.TryGetValue("Index", out var indexValue) && !indexValue.TryRead(out index))
        {
            LOG.LogWarning("The configured introduction {IntroductionIndex} does not contain a valid Index field. The Index must be an integer.", idx);
            return false;
        }

        introduction = new DataIntroduction
        {
            Id = id.ToString(),
            Title = title,
            VersionText = versionText,
            Index = index,
            Markdown = AIStudio.Tools.Markdown.RemoveSharedIndentation(markdown),
            EnterpriseConfigurationPluginId = configPluginId,
        };

        return true;
    }
}