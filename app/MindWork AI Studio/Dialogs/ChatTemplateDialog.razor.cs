using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.ToolCallingSystem;

using Lua;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ChatTemplateDialog : MSGComponentBase
{
    [Parameter]
    public LuaTable? ImportedConfiguration { get; set; }

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The chat template's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }

    /// <summary>
    /// The chat template's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The chat template name chosen by the user.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;

    /// <summary>
    /// What is the system prompt?
    /// </summary>
    [Parameter]
    public string DataSystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// What is the predefined user prompt?
    /// </summary>
    [Parameter]
    public string PredefinedUserPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }

    [Parameter]
    public bool IsReadOnly { get; init; }

    [Parameter]
    public IReadOnlyCollection<ContentBlock> ExampleConversation { get; init; } = [];

    [Parameter]
    public IReadOnlyCollection<FileAttachment> FileAttachments { get; init; } = [];

    [Parameter]
    public bool AllowProfileUsage { get; set; } = true;

    /// <summary>
    /// The tools this template preselects, or null when it says nothing about tools.
    /// </summary>
    [Parameter]
    public HashSet<string>? ToolIds { get; set; }

    /// <summary>
    /// The data source options this template preselects, or null when it says nothing about them.
    /// </summary>
    [Parameter]
    public DataSourceOptions? DataSourceOptions { get; set; }

    [Parameter]
    public bool CreateFromExistingChatThread { get; set; }

    [Parameter]
    public ChatThread? ExistingChatThread { get; set; }

    [Inject]
    private ILogger<ChatTemplateDialog> Logger { get; init; } = null!;

    [Inject]
    private ToolRegistry ToolRegistry { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    /// <summary>
    /// The list of used chat template names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedNames { get; set; } = [];

    private bool dataIsValid;
    private List<ContentBlock> dataExampleConversation = [];
    private HashSet<FileAttachment> fileAttachments = [];
    private List<(string OriginalPath, string ReplacementPath)> attachmentsToRelink = [];
    private string relinkIssue = string.Empty;
    private bool HasUnresolvedAttachments => this.attachmentsToRelink.Any(attachment => !ConfigurationImportFields.IsExistingLocalFile(attachment.ReplacementPath));
    private string importReferenceIssue = string.Empty;
    private bool preselectTools;
    private HashSet<string> selectedToolIds = new(StringComparer.Ordinal);
    private bool preselectDataSources;
    private DataSourceOptions templateDataSourceOptions = new();
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;
    private bool isInlineEditOnGoing;

    private ContentBlock? messageEntryBeforeEdit;

    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);

        // Load the used instance names:
        this.UsedNames = this.SettingsManager.ConfigurationData.ChatTemplates.Select(x => x.Name.ToLowerInvariant()).ToList();

        //
        // The two switches below carry the third state of the preselection: switched off, this
        // template says nothing, and a chat started with it uses the defaults from the chat
        // options. Their working copies live apart from the parameters, so switching a
        // preselection off and on again does not throw away what was picked.
        //
        this.preselectTools = this.ToolIds is not null;
        this.selectedToolIds = this.ToolIds is null ? new(StringComparer.Ordinal) : new(this.ToolIds, StringComparer.Ordinal);
        this.preselectDataSources = this.DataSourceOptions is not null;

        // Saying that this template preselects data sources is already the statement that it wants
        // them, so the switch inside the selection starts on instead of at its usual default:
        this.templateDataSourceOptions = this.DataSourceOptions?.CreateCopy() ?? new DataSourceOptions { DisableDataSources = false };

        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousName = this.DataName.ToLowerInvariant();
            this.dataExampleConversation = this.ExampleConversation.Select(n => n.DeepClone()).ToList();
            this.fileAttachments = [..this.FileAttachments];
        }

        if (this.CreateFromExistingChatThread && this.ExistingChatThread is not null)
        {
            this.DataSystemPrompt = this.ExistingChatThread.SystemPrompt;
            this.dataExampleConversation = this.ExistingChatThread.Blocks.Select(n => n.DeepClone(true)).ToList();
            this.DataName = this.ExistingChatThread.Name;
        }

        if (this.ImportedConfiguration is not null && this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES"))
            await this.ImportConfiguration(this.ImportedConfiguration);

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            this.form.ResetValidation();

        if (firstRender && this.ImportedConfiguration is not null &&
            !this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES"))
            this.MudDialog.Cancel();

        await base.OnAfterRenderAsync(firstRender);
    }

    #endregion

    private ChatTemplate CreateChatTemplateSettings() => new()
    {
        Num = this.DataNum,
        Id = this.DataId,

        Name = this.DataName,
        SystemPrompt = this.DataSystemPrompt,
        PredefinedUserPrompt = this.PredefinedUserPrompt,
        ExampleConversation = this.dataExampleConversation,
        FileAttachments = this.fileAttachments.Select(attachment => attachment.Normalize()).ToList(),
        AllowProfileUsage = this.AllowProfileUsage,
        ToolIds = this.preselectTools ? new HashSet<string>(this.selectedToolIds, StringComparer.Ordinal) : null,
        DataSourceOptions = this.preselectDataSources ? this.templateDataSourceOptions.CreateCopy() : null,

        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
    };

    private async Task ImportConfiguration(LuaTable table)
    {
        ConfigurationSnippetImportValidation.Validate("CHAT_TEMPLATES", table);
        if (!ChatTemplate.TryParseChatTemplateTable(0, table, Guid.Empty, string.Empty, out var parsed) || parsed is not ChatTemplate template)
            throw new FormatException(T("The chat template fields are malformed."));
        var paths = ConfigurationImportFields.Strings(table, "FileAttachments");
        var validAttachments = new HashSet<FileAttachment>();
        var toRelink = new List<(string OriginalPath, string ReplacementPath)>();
        foreach (var path in paths)
        {
            if (ConfigurationImportFields.IsExistingLocalFile(path))
                validAttachments.Add(FileAttachment.FromPath(path));
            else
                toRelink.Add((path, string.Empty));
        }

        this.DataName = template.Name;
        this.DataSystemPrompt = template.SystemPrompt;
        this.PredefinedUserPrompt = template.PredefinedUserPrompt;
        this.AllowProfileUsage = template.AllowProfileUsage;
        this.dataExampleConversation = template.ExampleConversation.Select(block => block.DeepClone()).ToList();
        this.fileAttachments = validAttachments;
        this.attachmentsToRelink = toRelink;
        this.preselectTools = template.ToolIds is not null;
        this.selectedToolIds = template.ToolIds is null ? new(StringComparer.Ordinal) : new(template.ToolIds, StringComparer.Ordinal);
        this.preselectDataSources = template.DataSourceOptions is not null;
        this.templateDataSourceOptions = template.DataSourceOptions?.CreateCopy() ?? new DataSourceOptions { DisableDataSources = false };
        this.importReferenceIssue = await this.BuildImportReferenceIssue();
    }

    private async Task<string> BuildImportReferenceIssue()
    {
        var missingSources = this.templateDataSourceOptions.PreselectedDataSourceIds
            .Where(id => this.SettingsManager.ConfigurationData.DataSources.All(source => source.Id != id)).ToList();
        var availableToolIds = (await this.ToolRegistry.GetCatalogAsync(AIStudio.Tools.Components.CHAT))
            .Select(item => item.Definition.Id).ToHashSet(StringComparer.Ordinal);
        var missingTools = this.selectedToolIds.Where(id => !availableToolIds.Contains(id)).ToList();
        var missing = missingSources.Select(ConfigurationImportFields.MissingDataSourceReference)
            .Concat(missingTools.Select(ConfigurationImportFields.MissingToolReference)).ToList();
        return ConfigurationImportFields.UnavailableReferencesIssue(missing);
    }

    private void UpdateRelinkPath(int index, string? path)
    {
        this.attachmentsToRelink[index] = (this.attachmentsToRelink[index].OriginalPath, path ?? string.Empty);
        if (!this.HasUnresolvedAttachments)
            this.relinkIssue = string.Empty;
    }

    private void RemoveAttachmentToRelink(int index)
    {
        this.attachmentsToRelink.RemoveAt(index);
        if (!this.HasUnresolvedAttachments)
            this.relinkIssue = string.Empty;
    }

    private void SetSelectedToolIds(HashSet<string> toolIds) => this.selectedToolIds = toolIds;

    private void RemoveMessage(ContentBlock item)
    {
        if (this.IsReadOnly)
            return;

        this.dataExampleConversation.Remove(item);
    }

    private void AddMessageToEnd()
    {
        if (this.IsReadOnly)
            return;

        var newEntry = new ContentBlock
        {
            Role = this.dataExampleConversation.Count is 0 ? ChatRole.USER : this.dataExampleConversation.Last().Role.SelectNextRoleForTemplate(),
            Content = new ContentText(),
            ContentType = ContentType.TEXT,
            HideFromUser = true,
            Time = DateTimeOffset.Now,
        };

        this.dataExampleConversation.Add(newEntry);
    }

    private void AddMessageBelow(ContentBlock currentItem)
    {
        if (this.IsReadOnly)
            return;

        var insertedEntry = new ContentBlock
        {
            Role = this.dataExampleConversation.Count is 0 ? ChatRole.USER : this.dataExampleConversation.Last().Role.SelectNextRoleForTemplate(),
            Content = new ContentText(),
            ContentType = ContentType.TEXT,
            HideFromUser = true,
            Time = DateTimeOffset.Now,
        };

        // The rest of the method remains the same:
        var index = this.dataExampleConversation.IndexOf(currentItem);
        if (index >= 0)
            this.dataExampleConversation.Insert(index + 1, insertedEntry);
        else
            this.dataExampleConversation.Add(insertedEntry);
    }

    private void BackupItem(object? element)
    {
        if (this.IsReadOnly)
            return;

        this.isInlineEditOnGoing = true;
        this.messageEntryBeforeEdit = element switch
        {
            ContentBlock block => block.DeepClone(),
            _ => null,
        };

        this.StateHasChanged();
    }

    private void ResetItem(object? element)
    {
        if (this.IsReadOnly)
            return;

        this.isInlineEditOnGoing = false;
        switch (element)
        {
            case ContentBlock block:
                if (this.messageEntryBeforeEdit is null)
                    return; // No backup to restore from

                block.Content = this.messageEntryBeforeEdit.Content?.DeepClone();
                block.Role = this.messageEntryBeforeEdit.Role;
                break;
        }

        this.StateHasChanged();
    }

    private void CommitInlineEdit(object? element)
    {
        if (this.IsReadOnly)
            return;

        this.isInlineEditOnGoing = false;
        this.StateHasChanged();
    }

    private async Task Store()
    {
        if (this.ImportedConfiguration is not null && !this.SettingsManager.ConfigurationData.App.CanImportConfigurationSnippet("CHAT_TEMPLATES"))
            return;

        if (this.IsReadOnly)
            return;

        // Only check the relinked attachments here. They are added right before closing, so that a
        // failed save does not leave a path behind which the user changes afterward:
        this.relinkIssue = string.Empty;
        foreach (var (originalPath, replacementPath) in this.attachmentsToRelink)
        {
            if (!ConfigurationImportFields.IsExistingLocalFile(replacementPath))
            {
                this.relinkIssue = string.Format(T("Relink the missing attachment '{0}' to an existing local file or remove it before saving."), originalPath);
                return;
            }
        }

        await this.form.Validate();

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        // When an inline edit is ongoing, we cannot store the data:
        if (this.isInlineEditOnGoing)
            return;

        foreach (var (_, replacementPath) in this.attachmentsToRelink)
            this.fileAttachments.Add(FileAttachment.FromPath(replacementPath));
        this.attachmentsToRelink.Clear();

        // Use the data model to store the chat template.
        // We just return this data to the parent component:
        var addedChatTemplateSettings = this.CreateChatTemplateSettings();

        if(this.IsEditing)
            this.Logger.LogInformation($"Edited chat template '{addedChatTemplateSettings.Name}'.");
        else
            this.Logger.LogInformation($"Created chat template '{addedChatTemplateSettings.Name}'.");

        this.MudDialog.Close(DialogResult.Ok(addedChatTemplateSettings));
    }

    private string? ValidateExampleTextMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return T("Please enter a message for the example conversation.");

        return null;
    }

    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("Please enter a name for the chat template.");

        if (name.Length > 40)
            return T("The chat template name must not exceed 40 characters.");

        // The instance name must be unique:
        var lowerName = name.ToLowerInvariant();
        if (lowerName != this.dataEditingPreviousName && this.UsedNames.Contains(lowerName))
            return T("The chat template name must be unique; the chosen name is already in use.");

        return null;
    }

    private void UseDefaultSystemPrompt()
    {
        if (this.IsReadOnly)
            return;

        this.DataSystemPrompt = SystemPrompts.DEFAULT;
    }

    private void Cancel() => this.MudDialog.Cancel();
}
