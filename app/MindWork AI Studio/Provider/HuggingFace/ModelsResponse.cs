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

/// <summary>
/// One model as the Hugging Face router describes it.
/// </summary>
/// <param name="Id">The ID of the model, written as "org/model".</param>
/// <param name="Providers">The inference providers serving this model.</param>
public readonly record struct HFModel(string Id, IList<HFModelProvider>? Providers);

/// <summary>
/// One inference provider serving a model.
/// </summary>
/// <param name="Provider">The slug of the inference provider, e.g. "novita".</param>
/// <param name="Status">Whether the provider currently serves the model. Known value: "live".</param>
public readonly record struct HFModelProvider(string Provider, string Status);

/// <summary>
/// One model as the Hugging Face hub lists it.
/// </summary>
/// <remarks>
/// The hub answers with a plain array of models and describes each of them in far more detail than
/// we need here, from tags to download counts. We only ever ask for the models of one provider and
/// one task, so the ID is all that is left to read.
/// </remarks>
/// <param name="Id">The ID of the model, written as "org/model".</param>
public readonly record struct HubModel(string Id);