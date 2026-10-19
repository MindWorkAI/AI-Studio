using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.Glossaries;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class GlossaryDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The glossary's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }

    /// <summary>
    /// The glossary's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The glossary name chosen by the user.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;

    /// <summary>
    /// The path of the glossary file.
    /// </summary>
    [Parameter]
    public string DataFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether the translation assistant uses the glossary right away.
    /// </summary>
    [Parameter]
    public bool DataPreselectInTranslation { get; set; }

    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }

    [Parameter]
    public bool IsReadOnly { get; init; }

    [Inject]
    private ILogger<GlossaryDialog> Logger { get; init; } = null!;

    [Inject]
    private GlossaryService GlossaryService { get; init; } = null!;

    [Inject]
    private PromptInjectionGuardService PromptInjectionGuardService { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    private static readonly FileTypeFilter[] FILE_FILTER = [FileTypes.SPREADSHEET, FileTypes.TABULAR];

    /// <summary>
    /// The list of used glossary names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedNames { get; set; } = [];

    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;
    private bool isCheckingFile;
    private GlossaryLoadResult? checkResult;

    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    private Glossary CreateGlossarySettings() => new()
    {
        Num = this.DataNum,
        Id = this.DataId,
        Name = this.DataName,
        FilePath = this.DataFilePath,
        PreselectInTranslation = this.DataPreselectInTranslation,

        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
    };

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the instance name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);

        // Load the used glossary names:
        this.UsedNames = this.SettingsManager.ConfigurationData.Glossaries.Select(x => x.Name.ToLowerInvariant()).ToList();

        // When editing, we need to load the data:
        if(this.IsEditing)
            this.dataEditingPreviousName = this.DataName.ToLowerInvariant();

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

    private void FilePathChanged(string filePath)
    {
        this.DataFilePath = filePath;

        // The result of the last check belongs to the previous file:
        this.checkResult = null;

        if (string.IsNullOrWhiteSpace(this.DataName))
            this.DataName = Path.GetFileNameWithoutExtension(filePath);
    }

    private async Task CheckFile()
    {
        if (string.IsNullOrWhiteSpace(this.DataFilePath) || this.isCheckingFile)
            return;

        this.isCheckingFile = true;
        this.checkResult = null;
        try
        {
            await using var reportingScope = this.PromptInjectionGuardService.BeginAction();
            this.checkResult = await this.GlossaryService.LoadAsync(this.DataFilePath, reportingScope);
        }
        finally
        {
            this.isCheckingFile = false;
        }
    }

    private string DescribeIssue(GlossaryLoadIssue issue) => issue switch
    {
        GlossaryLoadIssue.FILE_NOT_FOUND => T("The glossary file does not exist or is not reachable right now."),
        GlossaryLoadIssue.UNREADABLE => T("AI Studio could not read the glossary file. Please choose a spreadsheet or a CSV file."),
        GlossaryLoadIssue.NO_TABLE => T("The glossary file holds no usable table. Its first row must name at least two languages, and at least one further row must hold a term."),

        _ => string.Empty,
    };

    private async Task Store()
    {
        if (this.IsReadOnly)
            return;

        await this.form.Validate();

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        // Use the data model to store the glossary.
        // We just return this data to the parent component:
        var addedGlossarySettings = this.CreateGlossarySettings();

        if(this.IsEditing)
            this.Logger.LogInformation("Edited glossary '{GlossaryName}'.", addedGlossarySettings.Name);
        else
            this.Logger.LogInformation("Created glossary '{GlossaryName}'.", addedGlossarySettings.Name);

        this.MudDialog.Close(DialogResult.Ok(addedGlossarySettings));
    }

    private string? ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return T("Please choose the glossary file.");

        return null;
    }

    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("Please enter a glossary name.");

        if (name.Length > 40)
            return T("The glossary name must not exceed 40 characters.");

        // The glossary name must be unique:
        var lowerName = name.ToLowerInvariant();
        if (lowerName != this.dataEditingPreviousName && this.UsedNames.Contains(lowerName))
            return T("The glossary name must be unique; the chosen name is already in use.");

        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();
}