namespace AIStudio.Provider.Mistral;

public readonly record struct ModelsResponse(string Object, Model[] Data);

public readonly record struct Model(string Id, string Object, int Created, string OwnedBy);