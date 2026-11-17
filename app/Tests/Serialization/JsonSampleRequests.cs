using System.Text.Json;

using AIStudio.Provider;
using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Google;
using AIStudio.Provider.OpenAI;
using AIStudio.Tools.ToolCallingSystem;

using AnthropicImageUrl = AIStudio.Provider.Anthropic.SubContentImageUrl;
using OpenAIImageUrl = AIStudio.Provider.OpenAI.SubContentImageUrl;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// Requests to LLM providers as AI Studio builds them, with every kind of content they carry.
/// </summary>
/// <remarks>
/// The values are made up. What counts is that every part of a request holds one, so that its golden file shows
/// how each part gets written.
/// </remarks>
public static class JsonSampleRequests
{
    /// <summary>
    /// A tool with one required and one optional argument, the shape the web search has.
    /// </summary>
    public static ToolDefinition Tool() => new()
    {
        Id = "web_search",
        Function = new()
        {
            Name = "web_search",
            DescriptionForLLM = "Search the web.",
            Strict = true,
            Parameters = ToolParameterSchemaBuilder.Create()
                .RequiredString("query", "The search query.")
                .OptionalInteger("page", "The page, starting at 1.")
                .Build(),
        },
    };

    /// <summary>
    /// A request to a Chat Completions API, with a tool, a call of it, and its result.
    /// </summary>
    public static ChatCompletionAPIRequest ChatCompletion() => new("chat-model", Messages(), true)
    {
        Tools = [ProviderToolAdapters.ToChatCompletionTool(Tool(), hostEnforcesStrict: true)],
        ParallelToolCalls = false,
        AdditionalApiParameters = ApiParameters(),
    };

    /// <summary>
    /// A request to the Responses API of OpenAI, with an item of an earlier answer and the result of a tool.
    /// </summary>
    public static ResponsesAPIRequest Responses() => new("responses-model",
        [
            new TextMessage("You answer briefly.", "developer"),
            new MultimodalMessage(
            [
                new SubContentInputText(SubContentType.INPUT_TEXT, "What does the picture show?"),
                new SubContentInputImage(SubContentType.INPUT_IMAGE, "data:image/png;base64,iVBORw0KGgo="),
            ], "user"),
            Parse("""{"type":"function_call","id":"fc_1","call_id":"call_1","name":"web_search","arguments":"{\"query\":\"weather\"}"}"""),
            new ResponsesFunctionCallOutputItem { CallId = "call_1", Output = "It is sunny." },
        ],
        true, false, [ProviderToolAdapters.ToResponsesTool(Tool())])
    {
        AdditionalApiParameters = ApiParameters(),
    };

    /// <summary>
    /// A request to the Messages API of Anthropic, with images from a URL and as data, and a tool round.
    /// </summary>
    public static ChatRequest Anthropic() => new("messages-model",
        [
            new TextMessage("What is the weather?", "user"),
            new MultimodalMessage(
            [
                new SubContentText(SubContentType.TEXT, "Compare these pictures."),
                new SubContentImage(SubContentType.IMAGE, new SubContentBase64Image { MediaType = "image/png", Data = "iVBORw0KGgo=" }),
                new SubContentImage(SubContentType.IMAGE, new AnthropicImageUrl { Url = "https://example.org/picture.png" }),
            ], "user"),
            new AnthropicMessage(
            [
                Parse("""{"type":"text","text":"Let me search."}"""),
                Parse("""{"type":"tool_use","id":"toolu_1","name":"web_search","input":{"query":"weather"}}"""),
            ]),
            new AnthropicToolResultMessage([new AnthropicToolResultContent { ToolUseId = "toolu_1", Content = "It is sunny.", IsError = true }]),
        ],
        1024, true, "You answer briefly.")
    {
        Tools = [ProviderToolAdapters.ToAnthropicTool(Tool())],
        AdditionalApiParameters = ApiParameters(),
    };

    /// <summary>
    /// A request for embeddings in the shape of the OpenAI API.
    /// </summary>
    public static EmbeddingRequest Embedding() => new("embedding-model", ["The first text.", "The second text."]);

    /// <summary>
    /// A request for an embedding to the Gemini API.
    /// </summary>
    public static GoogleEmbeddingRequest GoogleEmbedding() => GoogleEmbeddingRequest.Of(["The first text.", "The second text."]);

    private static List<IMessageBase> Messages() =>
    [
        new TextMessage("You answer briefly.", "system"),
        new MultimodalMessage(
        [
            new SubContentText(SubContentType.TEXT, "What do the pictures show?"),
            new SubContentImageUrlNested(SubContentType.IMAGE_URL, new SubContentImageUrlData { Url = "data:image/png;base64,iVBORw0KGgo=" }),
            new OpenAIImageUrl(SubContentType.IMAGE_URL, "https://example.org/picture.png"),
        ], "user"),
        new AssistantToolCallMessage
        {
            Content = Parse("null"),
            ReasoningContent = "The weather needs a search.",
            ToolCalls =
            [
                new ChatCompletionToolCall
                {
                    Id = "call_1",
                    Function = new() { Name = "web_search", Arguments = """{"query":"weather"}""" },
                    AdditionalMetadata = { ["index"] = Parse("0") },
                },
            ],
        },
        new ToolResultMessage { Content = "It is sunny.", ToolCallId = "call_1" },
    ];

    /// <summary>
    /// Additional API parameters of every kind, read the way the provider settings read them.
    /// </summary>
    private static IDictionary<string, object> ApiParameters()
    {
        if (!AdditionalApiParametersParser.TryParse("""
                "temperature": 0.5,
                "top_k": 40,
                "seed": 12345678901,
                "stop": ["END", 7],
                "reasoning": { "effort": "low", "summary": null },
                "logprobs": false
                """, out var parameters, out var errorMessage))
            throw new InvalidOperationException(errorMessage);

        return parameters;
    }

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}