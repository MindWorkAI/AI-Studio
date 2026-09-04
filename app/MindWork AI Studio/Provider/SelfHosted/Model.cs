namespace AIStudio.Provider.SelfHosted;

public readonly record struct Model(string Id, string? Object, string? OwnedBy, ModelArchitecture? Architecture);