using System.Text.Json;

using AIStudio.Provider.Groq;
using AIStudio.Provider.OpenRouter;
using AIStudio.Provider.Requesty;

using MistralModelsResponse = AIStudio.Provider.Mistral.ModelsResponse;
using SelfHostedModelsResponse = AIStudio.Provider.SelfHosted.ModelsResponse;

namespace AIStudio.Tests.Provider;

/// <summary>
/// Checks that the numbers a provider already sends actually arrive in the records reading them.
/// </summary>
/// <remarks>
/// Every one of these providers spells the context window differently, and each record renames it
/// to the one word the app uses. Getting such a name wrong fails silently -- the field stays null,
/// the model list still loads, and the only symptom is a window nobody ever sees. The snippets
/// below are shortened answers of the real routes, so that a rename is caught here rather than by
/// somebody wondering why their window never shows up.
///
/// The options mirror what the providers deserialize with: names in snake case, which is what makes
/// the renaming attributes necessary in the first place.
/// </remarks>
[TestFixture]
public sealed class ModelListMetadataTests
{
    private static readonly JsonSerializerOptions AS_THE_PROVIDERS_READ_IT = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Test]
    // ReSharper disable once InconsistentNaming
    public void VLLMStatesTheWindowItWasStartedWith()
    {
        var response = JsonSerializer.Deserialize<SelfHostedModelsResponse>("""
                                                                           {
                                                                               "object": "list",
                                                                               "data": [
                                                                                   { "id": "Qwen/Qwen3-32B", "object": "model", "owned_by": "vllm", "max_model_len": 32768 }
                                                                               ]
                                                                           }
                                                                           """, AS_THE_PROVIDERS_READ_IT);

        Assert.That(response.Data, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(response.Data![0].ContextWindowTokens, Is.EqualTo(32_768));
            Assert.That(response.Data[0].OwnedBy, Is.EqualTo("vllm"), "Read with the shared options, this one arrives too -- it did not before.");
        });
    }

    [Test]
    public void AnEngineWhichStatesNoWindowLeavesItUnknown()
    {
        //
        // Ollama and LM Studio answer the very same route without that field. Nothing may fail
        // over it, and nothing may be invented for it either.
        //
        var response = JsonSerializer.Deserialize<SelfHostedModelsResponse>("""
                                                                           {
                                                                               "object": "list",
                                                                               "data": [ { "id": "gemma3:1b", "object": "model" } ]
                                                                           }
                                                                           """, AS_THE_PROVIDERS_READ_IT);

        Assert.That(response.Data, Is.Not.Null);
        Assert.That(response.Data![0].ContextWindowTokens, Is.Null);
    }

    [Test]
    public void OpenRouterStatesTheWindowOfTheModel()
    {
        var response = JsonSerializer.Deserialize<OpenRouterModelsResponse>("""
                                                                           {
                                                                               "data": [
                                                                                   {
                                                                                       "id": "anthropic/claude-sonnet-4.5",
                                                                                       "name": "Anthropic: Claude Sonnet 4.5",
                                                                                       "context_length": 1000000,
                                                                                       "architecture": { "tokenizer": "Claude" },
                                                                                       "top_provider": { "max_completion_tokens": 64000 }
                                                                                   }
                                                                               ]
                                                                           }
                                                                           """, AS_THE_PROVIDERS_READ_IT);

        Assert.Multiple(() =>
        {
            Assert.That(response.Data[0].ContextWindowTokens, Is.EqualTo(1_000_000));
            Assert.That(response.Data[0].Name, Is.EqualTo("Anthropic: Claude Sonnet 4.5"), "The fields we do read keep working next to the fields we deliberately do not.");
        });
    }

    [Test]
    public void GroqStatesTheWindowAsTheContextWindow()
    {
        var response = JsonSerializer.Deserialize<GroqModelsResponse>("""
                                                                     {
                                                                         "object": "list",
                                                                         "data": [ { "id": "llama-3.3-70b-versatile", "object": "model", "context_window": 131072 } ]
                                                                     }
                                                                     """, AS_THE_PROVIDERS_READ_IT);

        Assert.That(response.Data[0].ContextWindowTokens, Is.EqualTo(131_072));
    }

    [Test]
    public void RequestyStatesTheWindowAsTheContextWindow()
    {
        var response = JsonSerializer.Deserialize<RequestyModelsResponse>("""
                                                                         {
                                                                             "object": "list",
                                                                             "data": [ { "id": "openai/gpt-4o-mini", "object": "model", "api": "chat", "context_window": 128000, "max_output_tokens": 16384 } ]
                                                                         }
                                                                         """, AS_THE_PROVIDERS_READ_IT);

        Assert.That(response.Data[0].ContextWindowTokens, Is.EqualTo(128_000));
    }

    [Test]
    public void MistralStatesTheWindowAsAMaximumLength()
    {
        var response = JsonSerializer.Deserialize<MistralModelsResponse>("""
                                                                        {
                                                                            "object": "list",
                                                                            "data": [ { "id": "mistral-large-latest", "object": "model", "created": 1700000000, "owned_by": "mistralai", "max_context_length": 131072 } ]
                                                                        }
                                                                        """, AS_THE_PROVIDERS_READ_IT);

        Assert.That(response.Data[0].ContextWindowTokens, Is.EqualTo(131_072));
    }

    [Test]
    public void TheRouterStatesAWindowPerInferenceProvider()
    {
        var response = JsonSerializer.Deserialize<AIStudio.Provider.HuggingFace.ModelsResponse>("""
                                                                                               {
                                                                                                   "data": [
                                                                                                       {
                                                                                                           "id": "deepseek-ai/DeepSeek-R1",
                                                                                                           "providers": [
                                                                                                               { "provider": "novita", "status": "live", "context_length": 64000 },
                                                                                                               { "provider": "together", "status": "live", "context_length": 128000 }
                                                                                                           ]
                                                                                                       }
                                                                                                   ]
                                                                                               }
                                                                                               """, AS_THE_PROVIDERS_READ_IT);

        Assert.That(response.Data[0].Providers, Is.Not.Null);
        Assert.That(response.Data[0].Providers![0].ContextWindowTokens, Is.EqualTo(64_000));
    }
}