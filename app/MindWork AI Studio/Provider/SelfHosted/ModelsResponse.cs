namespace AIStudio.Provider.SelfHosted;

public readonly record struct ModelsResponse(string Object, Model[] Data);

public readonly record struct Model(string Id, string Object, string OwnedBy);