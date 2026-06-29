using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using AIStudio.Chat;
using AIStudio.Settings;

namespace AIStudio.Provider.Transparency;

public abstract class ProviderTransparencyBase(ILogger logger) : BaseProvider(LLMProviders.TRANSPARENCY, new Uri("https://transparency.invalid/v1/"), ExternalHttpTrustPolicy.SYSTEM_TRUST_ONLY, logger)
{
    protected const string PREVIEW_NOTICE = "Transparency preview only. AI Studio generated this request locally and did not contact an external provider.";

    private static readonly JsonSerializerOptions PRETTY_JSON_SERIALIZER_OPTIONS = new()
    {
        WriteIndented = true,
    };

    protected readonly ILogger Logger = logger;

    public static readonly Model CHAT_PREVIEW_MODEL = new("transparency-preview", "Transparency Preview");

    public static readonly Model EMBEDDING_PREVIEW_MODEL = new("transparency-embedding-preview", "Transparency Embedding Preview");

    public static readonly Model TRANSCRIPTION_PREVIEW_MODEL = new("transparency-transcription-preview", "Transparency Transcription Preview");

    public override string Id => LLMProviders.TRANSPARENCY.ToName();

    public override string InstanceName { get; set; } = "Transparency";

    public override bool HasModelLoadingCapability => false;

    public override Task<ModelLoadResult> GetTextModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult(SuccessfulModelLoadResult([CHAT_PREVIEW_MODEL]));

    public override Task<ModelLoadResult> GetImageModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult(SuccessfulModelLoadResult([]));

    public override Task<ModelLoadResult> GetEmbeddingModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult(SuccessfulModelLoadResult([EMBEDDING_PREVIEW_MODEL]));

    public override Task<ModelLoadResult> GetTranscriptionModels(string? apiKeyProvisional = null, CancellationToken token = default) => Task.FromResult(SuccessfulModelLoadResult([TRANSCRIPTION_PREVIEW_MODEL]));

    public override async IAsyncEnumerable<ContentStreamChunk> StreamChatCompletion(Model chatModel, ChatThread chatThread, SettingsManager settingsManager, [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public override async IAsyncEnumerable<ImageURL> StreamImageCompletion(Model imageModel, string promptPositive, string promptNegative = FilterOperator.String.Empty, ImageURL referenceImageURL = default, [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public override Task<TranscriptionResult> TranscribeAudioAsync(Model transcriptionModel, string audioFilePath, SettingsManager settingsManager, CancellationToken token = default) => Task.FromResult(TranscriptionResult.Failure());

    public override Task<IReadOnlyList<IReadOnlyList<float>>> EmbedTextAsync(Model embeddingModel, SettingsManager settingsManager, CancellationToken token = default, params List<string> texts) => Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>([]);

    protected static Model NormalizeModel(Model model, Model fallbackModel)
    {
        if (model.IsSystemModel || string.IsNullOrWhiteSpace(model.Id))
            return fallbackModel;

        return model;
    }

    protected string BuildJsonRequestPreview(string requestPath, Model model, string rawJson, string? readableBreakdown = null, params (string Label, string Value)[] details)
    {
        var requestUri = new Uri(this.BaseUri, requestPath);
        var builder = new StringBuilder();
        builder.AppendLine(PREVIEW_NOTICE);
        builder.AppendLine();
        builder.AppendLine("**Request metadata**");
        builder.AppendLine($"- URL: `{requestUri}`");
        builder.AppendLine($"- Instance: `{this.InstanceName}`");
        builder.AppendLine($"- Model: `{model.Id}`");

        foreach (var (label, value) in details)
            builder.AppendLine($"- {label}: `{value}`");

        builder.AppendLine();
        builder.AppendLine("**Readable body**");
        if (!string.IsNullOrWhiteSpace(readableBreakdown))
        {
            builder.AppendLine(readableBreakdown.TrimEnd());
            builder.AppendLine();
        }
        builder.AppendLine("```json");
        builder.AppendLine(PrettyPrintJson(rawJson));
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("**Original unchanged JSON**");
        builder.AppendLine("```json");
        builder.AppendLine(rawJson);
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    protected string BuildMultipartRequestPreview(string requestPath, Model model, string audioFilePath, string mimeType)
    {
        var requestUri = new Uri(this.BaseUri, requestPath);
        var fileName = Path.GetFileName(audioFilePath);
        var readableBody = JsonSerializer.Serialize(new
        {
            model = model.Id,
            file = new
            {
                source_path = audioFilePath,
                file_name = fileName,
                mime_type = mimeType,
            },
        }, PRETTY_JSON_SERIALIZER_OPTIONS);

        var rawBody = $$"""
                        file=@"{{audioFilePath}}"; filename="{{fileName}}"; content-type="{{mimeType}}"
                        model={{model.Id}}
                        """;

        var builder = new StringBuilder();
        builder.AppendLine(PREVIEW_NOTICE);
        builder.AppendLine();
        builder.AppendLine("**Request metadata**");
        builder.AppendLine($"- URL: `{requestUri}`");
        builder.AppendLine($"- Instance: `{this.InstanceName}`");
        builder.AppendLine($"- Model: `{model.Id}`");
        builder.AppendLine($"- Audio file: `{audioFilePath}`");
        builder.AppendLine();
        builder.AppendLine("**Readable body**");
        builder.AppendLine("```json");
        builder.AppendLine(readableBody);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("**Original request body**");
        builder.AppendLine("This request does not use JSON. These are the multipart fields AI Studio prepared:");
        builder.AppendLine("```text");
        builder.AppendLine(rawBody);
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    protected static IReadOnlyList<IReadOnlyList<float>> CreateDummyEmbeddings(int count)
    {
        var vectorCount = Math.Max(count, 1);
        return Enumerable.Range(0, vectorCount)
            .Select(index => (IReadOnlyList<float>)new float[]
            {
                0.125f + index,
                0.25f + index,
                0.375f + index,
                0.5f + index,
                0.625f + index,
                0.75f + index,
                0.875f + index,
                1f + index,
            })
            .ToArray();
    }

    private static string PrettyPrintJson(string rawJson)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(rawJson);
            return JsonSerializer.Serialize(jsonDocument.RootElement, PRETTY_JSON_SERIALIZER_OPTIONS);
        }
        catch (JsonException)
        {
            return rawJson;
        }
    }
}
