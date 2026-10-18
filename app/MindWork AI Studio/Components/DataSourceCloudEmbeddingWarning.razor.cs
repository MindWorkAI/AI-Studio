using AIStudio.Settings.DataModel;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Warns that a data source sends its content to providers which are not self-hosted: its text to
/// the embedding provider, and its audio and video files to the transcription provider.
/// </summary>
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

    /// <summary>
    /// Whether the content goes to an embedding provider which is not self-hosted.
    /// </summary>
    [Parameter]
    public bool SendsEmbeddingsToCloud { get; set; } = true;

    /// <summary>
    /// The name of the transcription provider which is not self-hosted and receives the audio and
    /// video files of the data source, or empty when no media file leaves the computer.
    /// </summary>
    [Parameter]
    public string CloudTranscriptionProvider { get; set; } = string.Empty;

    private bool SendsMediaToCloudTranscription => !string.IsNullOrWhiteSpace(this.CloudTranscriptionProvider);

    private string LabelOn => this.SendsMediaToCloudTranscription
        ? T("Yes, please send my data to the external providers")
        : T("Yes, please send my data to the external embedding provider");

    private string LabelOff => this.SendsMediaToCloudTranscription
        ? T("No, I will choose other providers or files")
        : T("No, I will choose another embedding");

    private string TranscriptionWarningText => string.Format(
        T("Warning: Your transcription provider '{0}' is not self-hosted. Audio and video files this data source selects are sent to it to be transcribed, which can cost money. AI Studio keeps every transcript, so an unchanged recording is sent only once. MindWork AI Studio has no control over what that third party does with the recordings after they are sent."),
        this.CloudTranscriptionProvider);

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
