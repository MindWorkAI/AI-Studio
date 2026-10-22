namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// A data model for the response from the model endpoint of the Hugging Face router.
/// </summary>
/// <remarks>
/// The router says more about a model than the OpenAI model list does: which inference providers
/// serve it, and which kinds of input it takes. That is why this provider brings its own data model
/// instead of using the shared one.
/// </remarks>
/// <param name="Data">The models the router knows.</param>
public readonly record struct ModelsResponse(IList<HFModel> Data);