namespace AIStudio.Provider.HuggingFace;

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