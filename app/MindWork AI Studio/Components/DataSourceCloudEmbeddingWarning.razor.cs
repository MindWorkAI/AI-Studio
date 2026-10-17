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

    /// <remarks>
    /// Which files of a folder are sent depends on its file patterns and switches. The sentence does
    /// not repeat them: they are right next to it in the dialog, and a sentence per combination would
    /// have to change with every new setting.
    /// </remarks>
    private string GetSubjectText()
    {
        if (string.IsNullOrWhiteSpace(this.SourcePath))
            return this.DataSourceType switch
            {
                DataSourceType.LOCAL_DIRECTORY => T("Every file this data source selects from this folder"),
                DataSourceType.LOCAL_FILE => T("The selected file"),
                DataSourceType.MAILBOX => T("Every mail of this mailbox in the selected folder and period, together with the text of its attachments,"),
                _ => T("The selected data")
            };

        return this.DataSourceType switch
        {
            DataSourceType.LOCAL_DIRECTORY => string.Format(T("Every file this data source selects from the folder '{0}'"), this.SourcePath),
            DataSourceType.LOCAL_FILE => string.Format(T("The file '{0}'"), this.SourcePath),
            DataSourceType.MAILBOX => string.Format(T("Every mail of the mailbox '{0}' in the selected folder and period, together with the text of its attachments,"), this.SourcePath),
            _ => string.Format(T("The data source '{0}'"), this.SourcePath)
        };
    }
}
