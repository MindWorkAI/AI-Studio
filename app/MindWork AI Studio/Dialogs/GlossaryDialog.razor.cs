using AIStudio.Components;
using AIStudio.Settings;
using AIStudio.Tools.Glossaries;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class GlossaryDialog : MSGComponentBase
{
    private const char ALIAS_SEPARATOR = ';';
    private const int MAX_NAME_LENGTH = 80;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The glossary to edit; a new one when the dialog adds a glossary.
    /// </summary>
    [Parameter]
    public Glossary Glossary { get; set; } = new() { Id = Guid.NewGuid().ToString() };

    /// <summary>
    /// The entries of the glossary to edit.
    /// </summary>
    [Parameter]
    public IReadOnlyList<GlossaryEntry> Entries { get; set; } = [];

    /// <summary>
    /// Should the dialog be in editing mode?
    /// </summary>
    [Parameter]
    public bool IsEditing { get; init; }

    [Inject]
    private ILogger<GlossaryDialog> Logger { get; init; } = null!;

    [Inject]
    private IDialogService DialogService { get; init; } = null!;

    private static readonly Dictionary<string, object?> SPELLCHECK_ATTRIBUTES = new();

    /// <summary>
    /// The list of used glossary names. We need this to check for uniqueness.
    /// </summary>
    private List<string> usedNames = [];

    private string dataName = string.Empty;
    private GlossaryType dataType;
    private string dataDescription = string.Empty;
    private CommonLanguages dataSourceLanguage;
    private string dataCustomSourceLanguage = string.Empty;
    private CommonLanguages dataTargetLanguage;
    private string dataCustomTargetLanguage = string.Empty;
    private bool dataPreselectInTranslation;
    private List<EntryRow> rows = [];
    private string searchText = string.Empty;
    private bool showDetails;

    private bool dataIsValid;
    private string[] dataIssues = [];
    private string dataEditingPreviousName = string.Empty;

    private MudForm form = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.SettingsManager.InjectSpellchecking(SPELLCHECK_ATTRIBUTES);

        this.usedNames = this.SettingsManager.ConfigurationData.Glossaries.Select(x => x.Name.ToLowerInvariant()).ToList();

        this.dataName = this.Glossary.Name;
        this.dataType = this.Glossary.Type;
        this.dataDescription = this.Glossary.Description;
        this.dataSourceLanguage = this.Glossary.SourceLanguage;
        this.dataCustomSourceLanguage = this.Glossary.CustomSourceLanguage;
        this.dataTargetLanguage = this.Glossary.TargetLanguage;
        this.dataCustomTargetLanguage = this.Glossary.CustomTargetLanguage;
        this.dataPreselectInTranslation = this.Glossary.PreselectInTranslation;
        this.SetRows(this.Entries);

        if(this.IsEditing)
            this.dataEditingPreviousName = this.dataName.ToLowerInvariant();

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

    private string TermTitle => this.dataType is GlossaryType.TRANSLATION ? T("Source term") : T("Term");

    private string ValueTitle => this.dataType is GlossaryType.TRANSLATION ? T("Translation") : T("Explanation");

    // MudBlazor's own text reads "1-25 of 300", in English only:
    private string PagerInfoFormat => string.Format(T("{0}-{1} of {2}"), "{first_item}", "{last_item}", "{all_items}");

    private string ValuePlaceholder => this.dataType is GlossaryType.TRANSLATION ? T("Enter the translation") : T("Enter the explanation");

    private string PreselectLabel => this.dataType is GlossaryType.TRANSLATION
        ? T("Preselect in the translation assistant when you translate into its target language")
        : T("Preselect in the translation assistant");

    private string LanguageName(CommonLanguages language) => language is CommonLanguages.AS_IS ? T("Please select a language") : language.Name();

    /// <summary>
    /// Fills the table, and shows the columns of other forms and notes when any entry uses them.
    /// </summary>
    private void SetRows(IEnumerable<GlossaryEntry> entries)
    {
        this.rows = entries.Select(EntryRow.From).ToList();
        this.showDetails = this.rows.Any(row => row.Aliases.Length > 0 || row.Note.Length > 0);
    }

    private bool MatchesSearch(EntryRow row)
    {
        if (string.IsNullOrWhiteSpace(this.searchText))
            return true;

        var search = this.searchText.Trim();
        return row.Term.Contains(search, StringComparison.OrdinalIgnoreCase)
               || row.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
               || row.Aliases.Contains(search, StringComparison.OrdinalIgnoreCase)
               || row.Note.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void AddRow()
    {
        // The new row has to show up on the first page, whatever the user searched for:
        this.searchText = string.Empty;
        this.rows = [new EntryRow(), ..this.rows];
    }

    private void RemoveRow(EntryRow row) => this.rows = this.rows.Where(x => !ReferenceEquals(x, row)).ToList();

    private int CountDuplicateTerms() => this.rows
        .Where(row => !string.IsNullOrWhiteSpace(row.Term))
        .GroupBy(row => row.Term.Trim().ToLowerInvariant())
        .Count(group => group.Count() > 1);

    /// <summary>
    /// Turns the rows of the table into entries; rows without a term and a value are left out.
    /// </summary>
    private List<GlossaryEntry> CreateEntries() => this.rows
        .Where(row => !string.IsNullOrWhiteSpace(row.Term) && !string.IsNullOrWhiteSpace(row.Value))
        .Select(row => row.ToEntry())
        .ToList();

    private async Task ImportEntries()
    {
        var dialogParameters = new DialogParameters<GlossaryImportDialog>
        {
            { x => x.ExistingEntries, this.CreateEntries() },
        };

        var dialogReference = await this.DialogService.ShowAsync<GlossaryImportDialog>(T("Import Glossary Entries"), dialogParameters, DialogOptions.FULLSCREEN);
        var dialogResult = await dialogReference.Result;
        if (dialogResult is null || dialogResult.Canceled || dialogResult.Data is not GlossaryImportDialogResult importResult)
            return;

        this.searchText = string.Empty;
        this.SetRows(importResult.Entries);

        if (string.IsNullOrWhiteSpace(this.dataName))
            this.dataName = importResult.SuggestedName;

        if (this.dataSourceLanguage is CommonLanguages.AS_IS && importResult.SuggestedSourceLanguage is { } sourceLanguage)
            this.dataSourceLanguage = sourceLanguage;

        if (this.dataTargetLanguage is CommonLanguages.AS_IS && importResult.SuggestedTargetLanguage is { } targetLanguage)
            this.dataTargetLanguage = targetLanguage;
    }

    private async Task Store()
    {
        await this.form.Validate();
        
        if (!this.dataIsValid)
            return;
        
        // A row with only a term or only a value is unfinished. Storing would drop it, so the user
        // decides first. Completely empty rows are dropped silently, e.g. one added by mistake:
        var unfinishedRows = this.rows.Count(row => string.IsNullOrWhiteSpace(row.Term) != string.IsNullOrWhiteSpace(row.Value));
        if (unfinishedRows > 0)
        {
            this.dataIssues = [string.Format(T("{0} entries lack a term or a value. Please complete or delete them."), unfinishedRows)];
            return;
        }

        var isTranslation = this.dataType is GlossaryType.TRANSLATION;
        var glossary = this.Glossary with
        {
            Name = this.dataName.Trim(),
            Type = this.dataType,
            Description = this.dataDescription.Trim(),
            SourceLanguage = isTranslation ? this.dataSourceLanguage : CommonLanguages.AS_IS,
            CustomSourceLanguage = isTranslation ? this.dataCustomSourceLanguage : string.Empty,
            TargetLanguage = isTranslation ? this.dataTargetLanguage : CommonLanguages.AS_IS,
            CustomTargetLanguage = isTranslation ? this.dataCustomTargetLanguage : string.Empty,
            PreselectInTranslation = this.dataPreselectInTranslation,
            IsEnterpriseConfiguration = false,
            EnterpriseConfigurationPluginId = Guid.Empty,
        };

        if(this.IsEditing)
            this.Logger.LogInformation("Edited glossary '{GlossaryName}'.", glossary.Name);
        else
            this.Logger.LogInformation("Created glossary '{GlossaryName}'.", glossary.Name);

        this.MudDialog.Close(DialogResult.Ok(new GlossaryDialogResult(glossary, this.CreateEntries())));
    }

    private string? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return T("Please enter a glossary name.");

        if (name.Length > MAX_NAME_LENGTH)
            return string.Format(T("The glossary name must not exceed {0} characters."), MAX_NAME_LENGTH);

        // The glossary name must be unique:
        var lowerName = name.ToLowerInvariant();
        if (lowerName != this.dataEditingPreviousName && this.usedNames.Contains(lowerName))
            return T("The glossary name must be unique; the chosen name is already in use.");

        return null;
    }

    private string? ValidateLanguage(CommonLanguages language)
    {
        if (this.dataType is GlossaryType.TRANSLATION && language is CommonLanguages.AS_IS)
            return T("Please select a language.");

        return null;
    }

    private string? ValidateCustomLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return T("Please enter the name of the language.");

        return null;
    }

    private void Cancel() => this.MudDialog.Cancel();

    /// <summary>
    /// One row of the entry table, which the user edits cell by cell.
    /// </summary>
    public sealed class EntryRow
    {
        public string Term { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// The other forms of the term, separated by semicolons.
        /// </summary>
        public string Aliases { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;

        public static EntryRow From(GlossaryEntry entry) => new()
        {
            Term = entry.Term,
            Value = entry.Value,
            Aliases = string.Join($"{ALIAS_SEPARATOR} ", entry.Aliases ?? []),
            Note = entry.Note,
        };

        public GlossaryEntry ToEntry()
        {
            var aliases = this.Aliases.Split(ALIAS_SEPARATOR, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return new(this.Term.Trim(), this.Value.Trim(), aliases.Length > 0 ? aliases : null, this.Note.Trim());
        }
    }
}