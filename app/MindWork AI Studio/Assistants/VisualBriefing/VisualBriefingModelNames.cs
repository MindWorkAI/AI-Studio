using AIStudio.Provider;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Produces export-safe provider and model labels.
/// </summary>
internal static class VisualBriefingModelNames
{
    /// <summary>
    /// Returns the public provider family and configured model name.
    /// </summary>
    /// <param name="provider">The selected provider and model.</param>
    /// <returns>An export-safe provider and model label.</returns>
    internal static string ExportLabel(ProviderSettings provider) => $"{provider.UsedLLMProvider.ToName(translate: false)} — {ExportModelName(provider.Model)}";

    /// <summary>
    /// Reconstructs an export label from persisted build provenance.
    /// </summary>
    /// <param name="providerFamily">The persisted provider family.</param>
    /// <param name="model">The persisted model name.</param>
    /// <returns>An export-safe provider and model label.</returns>
    internal static string ExportLabel(string providerFamily, string model)
    {
        var providerName = Enum.TryParse<LLMProviders>(providerFamily, out var parsedProvider) ? parsedProvider.ToName(translate: false) : string.IsNullOrWhiteSpace(providerFamily) ? "Unknown provider" : providerFamily.Trim();
        var modelName = string.IsNullOrWhiteSpace(model) ? "model not reported" : model.Trim();

        return $"{providerName} — {modelName}";
    }

    /// <summary>
    /// Returns the configured display name, model ID, or provider-managed fallback.
    /// </summary>
    private static string ExportModelName(Model model)
    {
        if (!string.IsNullOrWhiteSpace(model.DisplayName))
            return model.DisplayName.Trim();

        if (model.IsSystemModel)
            return "provider-configured model";

        return string.IsNullOrWhiteSpace(model.Id) ? "model not reported" : model.Id.Trim();
    }
}
