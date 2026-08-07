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

    private string GetSubjectText()
    {
        if (string.IsNullOrWhiteSpace(this.SourcePath))
            return this.DataSourceType switch
            {
                DataSourceType.LOCAL_DIRECTORY => T("All files in this folder and its subfolders"),
                DataSourceType.LOCAL_FILE => T("The selected file"),
                _ => T("The selected data")
            };

        return this.DataSourceType switch
        {
            DataSourceType.LOCAL_DIRECTORY => string.Format(T("All files in the folder '{0}' and its subfolders"), this.SourcePath),
            DataSourceType.LOCAL_FILE => string.Format(T("The file '{0}'"), this.SourcePath),
            _ => string.Format(T("The data source '{0}'"), this.SourcePath)
        };
    }
}
