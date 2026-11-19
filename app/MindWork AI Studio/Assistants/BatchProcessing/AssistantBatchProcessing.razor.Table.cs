using AIStudio.Tools.Rust;

namespace AIStudio.Assistants.BatchProcessing;

public partial class AssistantBatchProcessing
{
    private static readonly FileTypeFilter[] TABLE_FILE_TYPES = [FileTypes.CSV, FileTypes.TSV, FileTypes.SPREADSHEET];

    private string? ValidateTableFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return T("Please select the table whose rows or columns you want to process.");

        if (!File.Exists(path))
            return T("The selected table does not exist.");

        return null;
    }

    private async Task TableFilePathChanged(string path)
    {
        this.tableFilePath = path;
        await this.LoadTableSheetNamesAsync();
    }

    /// <summary>
    /// Lists the sheets of the selected workbook, so the user can pick the one to go through.
    /// </summary>
    /// <remarks>
    /// A CSV file has no sheets. Reading the workbook here only looks at its sheets and keeps nothing
    /// of its content, so passages the filter for prompt injections removes are reported when the run
    /// reads the workbook again, not here.
    /// </remarks>
    private async Task LoadTableSheetNamesAsync()
    {
        this.tableSheetNames = [];
        this.tableLoadIssue = string.Empty;
        if (string.IsNullOrWhiteSpace(this.tableFilePath) || BatchProcessingTable.IsCsvFile(this.tableFilePath) || !File.Exists(this.tableFilePath))
            return;

        this.isLoadingTableSheets = true;
        try
        {
            var extraction = await this.RustService.ReadArbitraryFileData(this.tableFilePath, int.MaxValue, reportPromptInjections: false);
            if (!extraction.HasUsableContent || extraction.Content is null)
            {
                this.tableLoadIssue = extraction.ToUserMessage(Path.GetFileName(this.tableFilePath));
                return;
            }

            this.tableSheetNames = [..BatchProcessingTable.ReadSheets(extraction.Content, isCsvFile: false).Select(sheet => sheet.Name)];
            if (!this.tableSheetNames.Contains(this.tableSheetName))
                this.tableSheetName = this.tableSheetNames.FirstOrDefault() ?? string.Empty;

            if (this.tableSheetNames.Count is 0)
                this.tableLoadIssue = T("The selected workbook contains no sheet which could be read.");
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Was not able to read the sheets of the batch input table '{TablePath}'.", this.tableFilePath);
            this.tableLoadIssue = string.Format(T("Was not able to read the table: {0}"), e.Message);
        }
        finally
        {
            this.isLoadingTableSheets = false;
        }
    }

    /// <summary>
    /// Reads the selected table and turns its rows or columns into the units of the run.
    /// </summary>
    /// <remarks>
    /// Read anew when the run starts instead of taking what the sheet list read: the table may have
    /// changed in the meantime, and this time, the user hears about passages the filter for prompt
    /// injections removed, since the values go to the model.
    /// </remarks>
    /// <returns>The units, or <c>null</c> when the run must not start.</returns>
    private async Task<IReadOnlyList<BatchProcessingUnit>?> FindTableUnitsAsync()
    {
        var fileName = Path.GetFileName(this.tableFilePath);
        var isCsvFile = BatchProcessingTable.IsCsvFile(this.tableFilePath);
        FileExtractionResult extraction;
        try
        {
            extraction = await this.RustService.ReadArbitraryFileData(this.tableFilePath, int.MaxValue);
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, "Was not able to read the batch input table '{TablePath}'.", this.tableFilePath);
            this.AddInputIssue(string.Format(T("Was not able to read the table: {0}"), e.Message));
            return null;
        }

        if (!extraction.HasUsableContent || extraction.Content is null)
        {
            this.Logger.LogError("Reading the batch input table '{TablePath}' failed: code={ErrorCode}, message='{ErrorMessage}'.", this.tableFilePath, extraction.ErrorCode, extraction.ErrorMessage);
            this.AddInputIssue(extraction.ToUserMessage(fileName));
            return null;
        }

        var sheets = BatchProcessingTable.ReadSheets(extraction.Content, isCsvFile);
        var sheet = isCsvFile ? sheets.FirstOrDefault() : sheets.FirstOrDefault(candidate => candidate.Name == this.tableSheetName);
        if (sheet is null)
        {
            this.AddInputIssue(isCsvFile || string.IsNullOrWhiteSpace(this.tableSheetName)
                ? T("Was not able to read any rows from the selected table.")
                : string.Format(T("The selected workbook no longer contains the sheet '{0}'."), this.tableSheetName));
            return null;
        }

        var entries = BatchProcessingTable.SplitIntoEntries(sheet.Rows, this.tableDirection);
        if (entries.Count is 0)
        {
            this.AddInputIssue(this.tableDirection is BatchProcessingTableDirection.ROWS
                ? T("The selected table has no row with values below its first row, which holds the headings.")
                : T("The selected table has no column with values next to its first column, which holds the headings."));
            return null;
        }

        return [..entries.Select(entry => new BatchProcessingUnit(this.tableFilePath, this.TableEntryName(fileName, sheet.Name, entry.Index), [], entry.Text))];
    }

    /// <summary>
    /// Names a row or column of the table, e.g., "customers.xlsx, Sheet1, row 5".
    /// </summary>
    /// <remarks>
    /// The name is the identity of the entry in the log and the results table, cf.
    /// BatchProcessingFileResult.RelativePath. It names the table and the sheet as well, because
    /// several tables next to each other, or several sheets of one workbook, share the default
    /// output folder, and a continued run must not take the answer for another table as its own.
    /// Rows and columns are numbered as a spreadsheet program shows them, as long as the table
    /// starts in its first cell.
    /// </remarks>
    private string TableEntryName(string fileName, string sheetName, int index)
    {
        var position = this.tableDirection is BatchProcessingTableDirection.ROWS
            ? string.Format(T("row {0}"), index + 1)
            : string.Format(T("column {0}"), BatchProcessingExcel.ColumnName(index));

        return string.IsNullOrWhiteSpace(sheetName)
            ? $"{fileName}, {position}"
            : $"{fileName}, {sheetName}, {position}";
    }

    /// <summary>
    /// The folder the default output folder lies in: the input folder, or the folder of the table.
    /// </summary>
    private string InputBaseDirectory => this.inputKind is BatchProcessingInputKind.TABLE
        ? Path.GetDirectoryName(this.tableFilePath) ?? string.Empty
        : this.inputDirectory;
}