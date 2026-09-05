namespace AIStudio.Provider.HuggingFace;

/// <summary>
/// One model as the Hugging Face router describes it.
/// </summary>
/// <param name="Id">The ID of the model, written as "org/model".</param>
/// <param name="Providers">The inference providers serving this model.</param>
public readonly record struct HFModel(string Id, IList<HFModelProvider>? Providers);