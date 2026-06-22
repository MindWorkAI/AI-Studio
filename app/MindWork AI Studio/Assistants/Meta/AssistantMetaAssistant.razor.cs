using AIStudio.Dialogs.Settings;

namespace AIStudio.Assistants.Meta;

public partial class AssistantMetaAssistant : AssistantBaseCore<NoSettingsPanel>
{
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
        return;
    }

    protected override bool MightPreselectValues()
    {
        return false;
    }
    
    private string AssembleSystemPrompt() => string.Empty;

    private async Task GenerateLuaAssistant()
    {
        this.CreateChatThread();
        var time = this.AddUserRequest(
            $"""
                Remind me to replace this placeholder with the real lua plugin context
             """);
        
        await this.AddAIResponseAsync(time);
    }

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
}
