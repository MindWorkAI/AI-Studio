using AIStudio.Provider;

using Host = AIStudio.Provider.SelfHosted.Host;

namespace AIStudio.Settings.DataModel.PreviousModels;

public readonly record struct Provider(
    uint Num,
    string Id,
    string InstanceName,
    LLMProviders UsedProvider,
    Model Model,
    bool IsSelfHosted = false,
    string Hostname = "http://localhost:1234",
    Host Host = Host.NONE);