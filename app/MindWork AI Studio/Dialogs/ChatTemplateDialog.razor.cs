using AIStudio.Chat;
using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class ChatTemplateDialog : MSGComponentBase
{
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

    /// <summary>
    /// The attachments of an imported chat template which are not on this computer, as the
    /// configuration snippet names them. The user relinks or removes each one before saving.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> AttachmentsToRelink { get; init; } = [];

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

    /// <summary>
    /// The profile this template preselects, the ID of the no-profile entry to start without one,
    /// or null when it says nothing about profiles.
    /// </summary>
    [Parameter]
    public string? ProfileId { get; set; }

    /// <summary>
    /// The provider this template preselects, or null when it says nothing about providers.
    /// </summary>
    [Parameter]
    public string? ProviderId { get; set; }

    /// <summary>
    /// The name of the workspace this template keeps its chats in, or null when it says nothing about workspaces.
    /// </summary>
    [Parameter]
    public string? WorkspaceName { get; set; }

    [Parameter]
    public bool CreateFromExistingChatThread { get; set; }

    [Parameter]
    public ChatThread? ExistingChatThread { get; set; }

    [Inject]
    private ILogger<ChatTemplateDialog> Logger { get; init; } = null!;

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
    private bool HasUnresolvedAttachments => this.attachmentsToRelink.Any(attachment => !IsExistingFile(attachment.ReplacementPath));
    private bool preselectTools;
    private HashSet<string> selectedToolIds = new(StringComparer.Ordinal);
    private bool preselectDataSources;
    private DataSourceOptions templateDataSourceOptions = new();
    private bool preselectProfile;
    private string selectedProfileId = Profile.NO_PROFILE.Id;
    private bool preselectProvider;
    private string selectedProviderId = string.Empty;
    private bool preselectWorkspace;
    private string selectedWorkspaceName = string.Empty;
    private IReadOnlyList<WorkspaceTreeWorkspace> availableWorkspaces = [];
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

        // The profile and the provider follow the same pattern, with a switch each:
        this.preselectProfile = this.ProfileId is not null;
        this.selectedProfileId = this.ProfileId ?? Profile.NO_PROFILE.Id;
        this.preselectProvider = this.ProviderId is not null;
        this.selectedProviderId = this.ProviderId ?? string.Empty;
        this.preselectWorkspace = this.WorkspaceName is not null;
        this.selectedWorkspaceName = this.WorkspaceName ?? string.Empty;

        // The existing workspaces are only offered to pick from; a name which does not exist yet is fine as well:
        var workspaceSnapshot = await WorkspaceBehaviour.GetOrLoadWorkspaceTreeShellAsync();
        this.availableWorkspaces = workspaceSnapshot.Workspaces;

        // When editing, we need to load the data:
        if(this.IsEditing)
            this.dataEditingPreviousName = this.DataName.ToLowerInvariant();

        // An edited template brings its conversation and attachments along, and so does an imported one:
        this.dataExampleConversation = this.ExampleConversation.Select(n => n.DeepClone()).ToList();
        this.fileAttachments = [..this.FileAttachments];
        this.attachmentsToRelink = this.AttachmentsToRelink.Select(path => (path, string.Empty)).ToList();

        if (this.CreateFromExistingChatThread && this.ExistingChatThread is not null)
        {
            this.DataSystemPrompt = this.ExistingChatThread.SystemPrompt;
            this.dataExampleConversation = this.ExistingChatThread.Blocks.Select(n => n.DeepClone(true)).ToList();
            this.DataName = this.ExistingChatThread.Name;
        }

        await base.OnInitializedAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Reset the validation when not editing and on the first render.
        // We don't want to show validation errors when the user opens the dialog.
        if(!this.IsEditing && firstRender)
            this.form.ResetValidation();

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

        // A template which forbids profiles has no use for a profile, so none is kept for it:
        ProfileId = this.AllowProfileUsage && this.preselectProfile ? this.selectedProfileId : null,
        ProviderId = this.preselectProvider && !string.IsNullOrWhiteSpace(this.selectedProviderId) ? this.selectedProviderId : null,
        WorkspaceName = this.preselectWorkspace && !string.IsNullOrWhiteSpace(this.selectedWorkspaceName) ? this.selectedWorkspaceName.Trim() : null,

        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
    };

    private static bool IsExistingFile(string path) => Path.IsPathFullyQualified(path) && File.Exists(path);

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

    /// <summary>
    /// Whether the preselected profile is gone, so that chats started with this template keep their usual profile.
    /// </summary>
    private bool IsPreselectedProfileMissing => this.preselectProfile
                                                && this.selectedProfileId != Profile.NO_PROFILE.Id
                                                && this.SettingsManager.GetProfileById(this.selectedProfileId) == Profile.NO_PROFILE;

    /// <summary>
    /// Whether the preselected provider is gone or may not be used for chats, so that chats started
    /// with this template keep their usual provider.
    /// </summary>
    /// <remarks>
    /// The list to pick from holds only the providers which may be used for chats. A template from
    /// another machine or from a configuration plugin may name another one, though, and the list
    /// alone would then show nothing about it.
    /// </remarks>
    private bool IsPreselectedProviderUnusable
    {
        get
        {
            if (!this.preselectProvider || string.IsNullOrWhiteSpace(this.selectedProviderId))
                return false;

            var provider = this.SettingsManager.GetProviderById(this.selectedProviderId);
            return provider == AIStudio.Settings.Provider.NONE || !this.SettingsManager.IsProviderConfident(provider, Tools.Components.CHAT);
        }
    }

    /// <summary>
    /// Whether workspaces are switched off, so that a workspace of this template has no effect for now.
    /// </summary>
    private bool AreWorkspacesDisabled => this.SettingsManager.ConfigurationData.Workspace.StorageBehavior is WorkspaceStorageBehavior.DISABLE_WORKSPACES;

    //
    // Picking an existing workspace fills the name field. Clearing the select must not wipe a name
    // the user typed, though, so an empty selection is ignored:
    //
    private void SelectExistingWorkspace(string workspaceName)
    {
        if (string.IsNullOrWhiteSpace(workspaceName))
            return;

        this.selectedWorkspaceName = workspaceName;
    }

    private string? ValidateWorkspaceName(string workspaceName)
    {
        if (this.preselectWorkspace && string.IsNullOrWhiteSpace(workspaceName))
            return T("Please enter the workspace for the chats of this template, or switch the preselection off.");

        return null;
    }

    private string? ValidatePreselectedProvider(string providerId)
    {
        if (this.preselectProvider && string.IsNullOrWhiteSpace(providerId))
            return T("Please choose the provider this chat template preselects, or switch the preselection off.");

        return null;
    }

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
        if (this.IsReadOnly)
            return;

        // Only check the relinked attachments here. They are added right before closing, so that a
        // failed save does not leave a path behind which the user changes afterward:
        this.relinkIssue = string.Empty;
        foreach (var (originalPath, replacementPath) in this.attachmentsToRelink)
        {
            if (!IsExistingFile(replacementPath))
            {
                this.relinkIssue = string.Format(T("Please relink the attachment '{0}' to a file on this computer or remove it before you save."), originalPath);
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