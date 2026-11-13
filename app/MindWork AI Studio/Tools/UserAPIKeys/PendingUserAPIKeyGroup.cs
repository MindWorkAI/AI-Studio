using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.UserAPIKeys;

/// <summary>
/// The providers of one configuration plugin which wait for the API key of the user. They share one key.
/// </summary>
/// <param name="ConfigPluginId">The ID of the configuration plugin.</param>
/// <param name="Prompt">What the configuration plugin tells the user about obtaining the key, if anything.</param>
/// <param name="Targets">The providers which receive the key.</param>
public sealed record PendingUserAPIKeyGroup(Guid ConfigPluginId, DataUserAPIKeyPrompt? Prompt, IReadOnlyList<PendingUserAPIKeyTarget> Targets);