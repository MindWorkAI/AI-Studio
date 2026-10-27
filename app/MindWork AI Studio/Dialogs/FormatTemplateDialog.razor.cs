using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class FormatTemplateDialog : MSGComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The format template's number in the list.
    /// </summary>
    [Parameter]
    public uint DataNum { get; set; }

    /// <summary>
    /// The format template's ID.
    /// </summary>
    [Parameter]
    public string DataId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The format template name chosen by the user.
    /// </summary>
    [Parameter]
    public string DataName { get; set; } = string.Empty;

    /// <summary>
    /// The absolute path of the template file.
    /// </summary>
    [Parameter]
    public string DataFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }

    [Parameter]
    public bool IsReadOnly { get; init; }

    [Inject]
    private ILogger<FormatTemplateDialog> Logger { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    /// <summary>
    /// The list of used format template names. We need this to check for uniqueness.
    /// </summary>
    private List<string> UsedNames { get; set; } = [];

    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;

    // We get the form reference from Blazor code to validate it manually:
    private MudForm form = null!;

    /// <summary>
    /// Returns the parameters which show the given format template in this dialog.
    /// </summary>
    /// <param name="formatTemplate">The format template to show.</param>
    /// <param name="isEditing">Whether the dialog changes a stored format template rather than adding one.</param>
    /// <param name="isReadOnly">Whether the dialog only shows the format template, e.g., one managed by the organization.</param>
    /// <returns>The parameters of the dialog.</returns>
    public static DialogParameters<FormatTemplateDialog> ParametersOf(FormatTemplate formatTemplate, bool isEditing, bool isReadOnly = false) => new()
    {
        { x => x.DataNum, formatTemplate.Num },
        { x => x.DataId, formatTemplate.Id },
        { x => x.DataName, formatTemplate.Name },
        { x => x.DataFilePath, formatTemplate.FilePath },
        { x => x.IsEditing, isEditing },
        { x => x.IsReadOnly, isReadOnly },
    };

    private FormatTemplate CreateFormatTemplateSettings() => new()
    {
        Num = this.DataNum,
        Id = this.DataId,
        Name = this.DataName,
        FilePath = this.DataFilePath,

        EnterpriseConfigurationPluginId = Guid.Empty,
        IsEnterpriseConfiguration = false,
    };

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Configure the spellchecking for the name input:
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);

        // Load the used names:
        this.UsedNames = this.SettingsManager.ConfigurationData.FormatTemplates.Select(x => x.Name.ToLowerInvariant()).ToList();

        // When editing, we need to load the data:
        if(this.IsEditing)
        {
            this.dataEditingPreviousName = this.DataName.ToLowerInvariant();
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

    private async Task Store()
    {
        if (this.IsReadOnly)
            return;

        await this.form.Validate();

        // When the data is not valid, we don't store it:
        if (!this.dataIsValid)
            return;

        // Use the data model to store the format template.
        // We just return this data to the parent component:
        var addedFormatTemplateSettings = this.CreateFormatTemplateSettings();

        if(this.IsEditing)
            this.Logger.LogInformation($"Edited format template '{addedFormatTemplateSettings.Name}'.");
        else
            this.Logger.LogInformation($"Created format template '{addedFormatTemplateSettings.Name}'.");

        this.MudDialog.Close(DialogResult.Ok(addedFormatTemplateSettings));
    }

    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("Please enter a name for the format template.");

        if (name.Length > 40)
            return T("The name of the format template must not exceed 40 characters.");

        // The name must be unique:
        var lowerName = name.ToLowerInvariant();
        if (lowerName != this.dataEditingPreviousName && this.UsedNames.Contains(lowerName))
            return T("The name of the format template must be unique; the chosen name is already in use.");

        return null;
    }

    private string? ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return T("Please select a template file.");

        // Checked before the file itself, so that a wrong choice on a network share is not looked up:
        if (!FileTypes.IsAllowedPath(filePath, FileTypes.FORMAT_TEMPLATE))
            return string.Format(T("The selected file is not a supported format template. Supported file types are: {0}."), string.Join(", ", FileTypes.FORMAT_TEMPLATE.FlattenExtensions().Select(extension => $".{extension}")));

        if (!File.Exists(filePath))
            return T("The template file does not exist.");

        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();
}