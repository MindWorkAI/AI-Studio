using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class DataSourceCloudEmbeddingWarning : MSGComponentBase
{
    [Parameter]
    public DataSourceType DataSourceType { get; set; }

    [Parameter]
    public string SourcePath { get; set; } = string.Empty;

    [Parameter]
    public bool IncludeSubdirectories { get; set; } = true;

    [Parameter]
    public bool IncludeHiddenSubdirectories { get; set; }

    [Parameter]
    public bool UserAcknowledged { get; set; }

    [Parameter]
    public EventCallback<bool> UserAcknowledgedChanged { get; set; }

    [Parameter]
    public Func<bool, string?> Validation { get; set; } = _ => null;

    private string WarningText
    {
        get
        {
            var subject = this.GetSubjectText();
            return string.Format(
                T("Warning: The selected embedding provider is not self-hosted. Creating embeddings can cost money and may need to run multiple times, for example after errors or file changes. {0} will be sent to an external third party. MindWork AI Studio has no control over what that third party does with the data after it is sent."),
                subject);
        }
    }

    private string GetSubjectText()
    {
        if (string.IsNullOrWhiteSpace(this.SourcePath))
            return this.DataSourceType switch
            {
                DataSourceType.LOCAL_DIRECTORY when this.IncludeSubdirectories && this.IncludeHiddenSubdirectories => T("Files matching the configured patterns in this folder and its subfolders, including hidden subfolders"),
                DataSourceType.LOCAL_DIRECTORY when this.IncludeSubdirectories => T("Files matching the configured patterns in this folder and its non-hidden subfolders"),
                DataSourceType.LOCAL_DIRECTORY => T("Files matching the configured patterns directly in this folder"),
                DataSourceType.LOCAL_FILE => T("The selected file"),
                DataSourceType.MAILBOX => T("Every mail of this mailbox in the selected folder and period, together with the text of its attachments,"),
                _ => T("The selected data")
            };

        return this.DataSourceType switch
        {
            DataSourceType.LOCAL_DIRECTORY when this.IncludeSubdirectories && this.IncludeHiddenSubdirectories => string.Format(T("Files matching the configured patterns in the folder '{0}' and its subfolders, including hidden subfolders"), this.SourcePath),
            DataSourceType.LOCAL_DIRECTORY when this.IncludeSubdirectories => string.Format(T("Files matching the configured patterns in the folder '{0}' and its non-hidden subfolders"), this.SourcePath),
            DataSourceType.LOCAL_DIRECTORY => string.Format(T("Files matching the configured patterns directly in the folder '{0}'"), this.SourcePath),
            DataSourceType.LOCAL_FILE => string.Format(T("The file '{0}'"), this.SourcePath),
            DataSourceType.MAILBOX => string.Format(T("Every mail of the mailbox '{0}' in the selected folder and period, together with the text of its attachments,"), this.SourcePath),
            _ => string.Format(T("The data source '{0}'"), this.SourcePath)
        };
    }
}
