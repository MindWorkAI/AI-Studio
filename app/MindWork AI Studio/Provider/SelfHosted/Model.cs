using System.Text.Json.Serialization;

namespace AIStudio.Provider.SelfHosted;

/// <summary>
/// One model as an OpenAI-compatible engine lists it.
/// </summary>
/// <remarks>
/// The context window is vLLM's addition to that route: it reports the window the operator started
/// the engine with, which is the one number no rule about the weights could ever know. Ollama,
/// LM Studio, and llama.cpp answer the same route without it, so it stays unknown there instead of
/// being guessed.
///
/// vLLM calls that field max_model_len, which reads like a limit on the model rather than on a
/// conversation. The wire keeps their spelling, and this record says what the number means, so that
/// nobody has to remember the translation while reading the code that uses it.
/// </remarks>
/// <param name="Id">The model's ID.</param>
/// <param name="Object">What kind of thing the entry is. Known value: "model".</param>
/// <param name="OwnedBy">Who the engine names as the owner of the model.</param>
/// <param name="Architecture">Which kinds of input and output the model takes, where the engine says.</param>
/// <param name="ContextWindowTokens">The context window the engine was started with, in tokens, where it says.</param>
public readonly record struct Model(string Id, string? Object, string? OwnedBy, ModelArchitecture? Architecture, [property: JsonPropertyName("max_model_len")] int? ContextWindowTokens);