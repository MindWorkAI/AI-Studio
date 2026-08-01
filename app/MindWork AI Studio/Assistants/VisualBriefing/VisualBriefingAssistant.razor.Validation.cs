using AIStudio.Provider;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

public partial class VisualBriefingAssistant
{
    /// <summary>Gets whether the briefing contains at least one actual source-material file.</summary>
    private bool HasSourceMaterial => this.selectedBriefing?.Sources.Any(source => source.Kind is VisualBriefingSourceKind.SOURCE_MATERIAL) == true;

    /// <summary>Gets all current field, source, and revision issues shown below the actions.</summary>
    private IReadOnlyList<string> ValidationIssues
    {
        get
        {
            List<string> issues = [.. this.formIssues];

            AddIssue(issues, this.ValidateProjectName(this.projectName));
            AddIssue(issues, this.ValidateProvider(this.provider));
            AddIssue(issues, this.ValidateCustomTargetLanguage(this.customTargetLanguage));
            AddIssue(issues, this.ValidateCustomProtectionLevel(this.customProtectionLevel));

            if (this.selectedBriefing is not null)
            {
                if (!this.HasSourceMaterial)
                    issues.Add(T("Please add at least one source material file."));

                foreach (var source in this.selectedBriefing.Sources)
                {
                    var fileName = Path.GetFileName(source.Path);
                    switch (source.Status)
                    {
                        case VisualBriefingSourceStatus.UNREACHABLE:
                            issues.Add(string.Format(T("The source '{0}' is no longer reachable. Restore or relink it."), fileName));
                            break;

                        case VisualBriefingSourceStatus.TRANSCRIPT_OUTDATED:
                            issues.Add(string.Format(T("The transcript for '{0}' is missing or outdated. Transcribe the media source again."), fileName));
                            break;
                    }
                }

                if (this.selectedBriefing.Versions.Count > 0 && !this.SelectedVersionSupportsEdits)
                    issues.Add(T("This version has no compatible semantic artifacts. Rebuild the briefing instead."));
            }

            return [.. issues.Where(issue => !string.IsNullOrWhiteSpace(issue)).Distinct(StringComparer.Ordinal)];
        }
    }

    /// <summary>Validates the briefing name.</summary>
    private string? ValidateProjectName(string name) => string.IsNullOrWhiteSpace(name) ? T("Please provide a briefing name.") : null;

    /// <summary>Validates the selected generation provider.</summary>
    private string? ValidateProvider(ProviderSettings value) =>
        value == ProviderSettings.NONE || value.UsedLLMProvider is LLMProviders.NONE
            ? T("Please select a provider.")
            : null;

    /// <summary>Validates the free-form target language when Other is selected.</summary>
    private string? ValidateCustomTargetLanguage(string language) =>
        this.targetLanguage is CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language)
            ? T("Please provide a custom target language.")
            : null;

    /// <summary>Validates the free-form protection level when Other is selected.</summary>
    private string? ValidateCustomProtectionLevel(string level) =>
        this.protectionLevel is VisualBriefingProtectionLevel.OTHER && string.IsNullOrWhiteSpace(level)
            ? T("Please provide a custom protection level.")
            : null;

    /// <summary>Revalidates after a conditional Other field has been added or removed.</summary>
    private Task ScheduleFormValidation()
    {
        this.formValidationPending = true;
        this.StateHasChanged();
        
        return Task.CompletedTask;
    }

    /// <summary>Adds one optional validation message.</summary>
    private static void AddIssue(ICollection<string> issues, string? issue)
    {
        if (!string.IsNullOrWhiteSpace(issue))
            issues.Add(issue);
    }
}