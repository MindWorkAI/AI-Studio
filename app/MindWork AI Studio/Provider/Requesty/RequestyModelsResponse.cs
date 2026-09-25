namespace AIStudio.Provider.Requesty;

/// <summary>
/// A data model for the response from the Requesty models endpoints.
/// </summary>
/// <param name="Data">The list of models.</param>
public readonly record struct RequestyModelsResponse(IList<RequestyModel> Data);