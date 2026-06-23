using System.Text;
using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.Meta;

public partial class AssistantMetaAssistant : AssistantBaseCore<NoSettingsPanel>
{
    private static readonly ILogger LOGGER = Program.LOGGER_FACTORY.CreateLogger(nameof(AssistantMetaAssistant));

    protected override Tools.Components Component => Tools.Components.META_ASSISTANT;
    protected override string Title => T("Assistant Builder");
    protected override string Description => string.Empty;
    protected override string SystemPrompt => this.AssembleSystemPrompt();
    protected override string SubmitText => T("Build your assistant");
    protected override Func<Task> SubmitAction => this.GenerateLuaAssistant;
    protected override bool SubmitDisabled => this.isAgentRunning;
    protected override bool ShowResult { get; }
    protected override bool AllowProfiles { get; }
    protected override bool ShowProfileSelection { get; }
    protected override IReadOnlyList<IButtonData> FooterButtons => [];
    protected override bool HasSettingsPanel { get; }

    private bool isAgentRunning;
    private AssistantCategory selectedCategory;
    private string customCategory = string.Empty;
    private static readonly AssistantContextFile[] ASSISTANT_CONTEXT_FILES =
    [
        new("Assistant plugin schema", "Plugins/assistants/README.md", IsRequired: true),
        new("Lua manifest template", "Plugins/assistants/plugin.lua", IsRequired: true),
        new("Translation example", "Plugins/assistants/examples/translation/plugin.lua", IsRequired: false),
    ];
    private readonly record struct AssistantContextFile(
        string Title,
        string RelativePath,
        bool IsRequired);

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
    }

    #endregion

    protected override void ResetForm()
    {
        this.selectedCategory = AssistantCategory.AS_IS;
        this.customCategory = string.Empty;
    }

    protected override bool MightPreselectValues()
    {
        return false;
    }

    private string AssembleSystemPrompt() => string.Empty;

    private string? ValidatingCategory(AssistantCategory category)
    {
        if(category is AssistantCategory.AS_IS)
            return T("Please select an assistant category.");

        return null;
    }

    private string? ValidateCustomCategory(string category)
    {
        if(this.selectedCategory is AssistantCategory.OTHER && string.IsNullOrWhiteSpace(category))
            return T("Please provide a custom category.");

        return null;
    }

    private async Task GenerateLuaAssistant()
    {
        await this.Form!.Validate();
        if (!this.InputIsValid)
            return;

        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                Assistant category: {this.GetSelectedCategoryName()}

                Remind me to replace this placeholder with the real lua plugin context
             """);

        await this.AddAIResponseAsync(time);
    }

    private string GetSelectedCategoryName() => this.selectedCategory is AssistantCategory.OTHER
        ? this.customCategory
        : this.selectedCategory.Name();

    private static async Task<string> ReadAppResourceTextAsync(string relativePath)
    {
        relativePath = relativePath.Replace('\\', '/');
#if DEBUG
        var filePath = Path.Join(Environment.CurrentDirectory, relativePath);
        return File.Exists(filePath)
            ? await File.ReadAllTextAsync(filePath)
            : string.Empty;
#else
        var provider = new ManifestEmbeddedFileProvider(Assembly.GetAssembly(type: typeof(Program))!);
        var file = provider.GetFileInfo(relativePath);
        if (!file.Exists)
            return string.Empty;

        await using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
#endif
    }

    private async Task<string> LoadAssistantBuilderContextAsync()
    {
        var builder = new StringBuilder();

        foreach (var contextFile in ASSISTANT_CONTEXT_FILES)
        {
            var content = await ReadAppResourceTextAsync(contextFile.RelativePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                LOGGER.LogError($"The context for \"{contextFile.Title}\" could not be read from the assembly. Path: {contextFile.RelativePath}");
                if (contextFile.IsRequired)
                {
                    await MessageBus.INSTANCE.SendError(new (Icons.Material.Filled.SettingsSuggest, string.Format(T("The Assistant-Builder was not able to read the plugin manifest and therefore cannot safely generate your assistant right now."))));
                    this.isAgentRunning = true;
                    return string.Empty;
                }
                continue;
            }

            builder.AppendLine($"# {contextFile.Title}");
            builder.AppendLine($"Source: {contextFile.RelativePath}");
            builder.AppendLine("<context>");
            builder.AppendLine(content.Trim());
            builder.AppendLine("</context>");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }
}
