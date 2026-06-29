using System.Text.Json;

using AIStudio.Settings;

namespace AIStudio.Provider.Transparency;

public sealed class ProviderTransparencyEmbedding() : ProviderTransparencyBase(LOGGER)
{
    private static readonly ILogger<ProviderTransparencyEmbedding> LOGGER = Program.LOGGER_FACTORY.CreateLogger<ProviderTransparencyEmbedding>();

    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts)
    {
        var effectiveModel = NormalizeModel(embeddingModel, EMBEDDING_PREVIEW_MODEL);
        var requestBody = JsonSerializer.Serialize(new
        {
            model = effectiveModel.Id,
            input = texts,
            encoding_format = "float",
        }, JSON_SERIALIZER_OPTIONS);

        var preview = this.BuildJsonRequestPreview(
            "embeddings",
            effectiveModel,
            requestBody,
            readableBreakdown: null,
            ("Input collection count", texts.Count.ToString()),
            ("Stream", bool.FalseString));

        this.Logger.LogInformation("Transparency embedding preview for '{ProviderInstanceName}' (provider={ProviderType}).{NewLine}{Preview}", this.InstanceName, this.Provider, Environment.NewLine, preview);
        return Task.FromResult(CreateDummyEmbeddings(texts.Count));
    }
}
