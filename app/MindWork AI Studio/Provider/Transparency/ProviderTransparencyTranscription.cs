using AIStudio.Settings;
using AIStudio.Tools.MIME;

namespace AIStudio.Provider.Transparency;

public sealed class ProviderTransparencyTranscription() : ProviderTransparencyBase(LOGGER)
{
    private static readonly ILogger<ProviderTransparencyTranscription> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderTransparencyTranscription>();

    public override Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default)
    {
        var effectiveModel = NormalizeModel(transcriptionModel, TRANSCRIPTION_PREVIEW_MODEL);
        var mimeType = Builder.FromFilename(audioFilePath);
        var preview = this.BuildMultipartRequestPreview("audio/transcriptions", effectiveModel, audioFilePath, mimeType);
        return Task.FromResult(TranscriptionResult.FromText(preview));
    }
}
