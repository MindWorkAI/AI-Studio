using AIStudio.Components;
using AIStudio.Tools.Glossaries;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Security;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Dialogs;

public partial class GlossaryImportDialog : MSGComponentBase
{
    private const int NO_COLUMN = -1;
    private const int PREVIEW_ROWS = 5;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// The entries the glossary holds before the import.
    /// </summary>
    [Parameter]
    public IReadOnlyList<GlossaryEntry> ExistingEntries { get; set; } = [];

    [Inject]
    private GlossaryImportService GlossaryImportService { get; init; } = null!;

    [Inject]
    private PromptInjectionGuardService PromptInjectionGuardService { get; init; } = null!;

    private static readonly FileTypeFilter[] FILE_FILTER = [FileTypes.SPREADSHEET, FileTypes.TABULAR];

    private string filePath = string.Empty;
    private bool isReadingFile;
    private GlossaryImportFile? importFile;
    private int selectedTableIndex;
    private int termColumn;
    private int valueColumn = 1;
    private int aliasesColumn = NO_COLUMN;
    private int noteColumn = NO_COLUMN;
    private GlossaryImportMode mode = GlossaryImportMode.MERGE;

    //
    // The preview runs the whole import. Blazor renders often, e.g. while the user opens a select,
    // so the preview is computed again only when one of its inputs changed:
    //
    private (int Table, int Term, int Value, int Aliases, int Note, GlossaryImportMode Mode)? previewKey;
    private GlossaryImportResult? previewResult;

    private GlossaryImportTable? SelectedTable => this.importFile is { Success: true } file ? file.Tables[this.selectedTableIndex] : null;

    private GlossaryImportResult? Preview
    {
        get
        {
            if (this.SelectedTable is not { } table || this.termColumn == this.valueColumn)
                return null;

            var key = (this.selectedTableIndex, this.termColumn, this.valueColumn, this.aliasesColumn, this.noteColumn, this.mode);
            if (this.previewKey != key)
            {
                this.previewResult = GlossaryImport.Apply(this.ExistingEntries, table, this.CreateMapping(), this.mode);
                this.previewKey = key;
            }

            return this.previewResult;
        }
    }

    private GlossaryColumnMapping CreateMapping() => new(
        this.termColumn,
        this.valueColumn,
        this.aliasesColumn == NO_COLUMN ? null : this.aliasesColumn,
        this.noteColumn == NO_COLUMN ? null : this.noteColumn);

    private async Task FilePathChanged(string newFilePath)
    {
        this.filePath = newFilePath;
        this.importFile = null;
        this.previewKey = null;
        if (string.IsNullOrWhiteSpace(newFilePath))
            return;

        this.isReadingFile = true;
        try
        {
            await using var reportingScope = this.PromptInjectionGuardService.BeginAction();
            this.importFile = await this.GlossaryImportService.ReadFileAsync(newFilePath, reportingScope);
            this.SelectedTableChanged(0);
        }
        finally
        {
            this.isReadingFile = false;
        }
    }

    private void SelectedTableChanged(int tableIndex)
    {
        this.selectedTableIndex = tableIndex;
        this.termColumn = 0;
        this.valueColumn = 1;
        this.aliasesColumn = NO_COLUMN;
        this.noteColumn = NO_COLUMN;
    }

    private string DescribeIssue(GlossaryImportIssue issue) => issue switch
    {
        GlossaryImportIssue.FILE_NOT_FOUND => T("The file does not exist or is not reachable right now."),
        GlossaryImportIssue.UNREADABLE => T("AI Studio could not read the file. Please choose a spreadsheet or a CSV file."),
        GlossaryImportIssue.NO_TABLE => T("The file holds no usable table. Its first row must name at least two columns, and at least one further row must hold an entry."),

        _ => string.Empty,
    };

    private void Import()
    {
        if (this.SelectedTable is not { } table || this.Preview is not { } preview)
            return;

        this.MudDialog.Close(DialogResult.Ok(new GlossaryImportDialogResult(
            preview.Entries,
            Path.GetFileNameWithoutExtension(this.filePath),
            GlossaryImport.SuggestLanguage(table.Columns[this.termColumn]),
            GlossaryImport.SuggestLanguage(table.Columns[this.valueColumn]))));
    }

    private void Cancel() => this.MudDialog.Cancel();
}