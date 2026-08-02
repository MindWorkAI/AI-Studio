using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.Rust;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

public partial class VisualBriefingAssistant
{
    /// <summary>Gets whether the briefing contains at least one actual source-material file.</summary>
    /// <remarks>
    /// This deliberately reads the stored manifest instead of the editor state: a build always runs
    /// against what the store accepted, and the store drops attachments whose file disappeared before
    /// the save. Every path that changes sources therefore has to save with a reload, otherwise this
    /// check keeps reporting the state from before the change.
    /// </remarks>
    private bool HasSourceMaterial => this.selectedBriefing?.Sources.Any(source => source.Kind is VisualBriefingSourceKind.SOURCE_MATERIAL) == true;

    /// <summary>Gets whether any stored source reaches the model as an image.</summary>
    /// <remarks>
    /// Both source kinds can end up as an image: source preparation converts every visual asset into an
    /// image attachment, and a source material file is attached as it is, where the attachment type is
    /// derived from the file extension alone. Checking the extension therefore covers both, and it
    /// matches the rule the attachment control already applies while a file is being added.
    /// </remarks>
    private bool HasImageSources => this.selectedBriefing?.Sources.Any(source => FileTypes.IsAllowedPath(source.Path, FileTypes.IMAGE)) == true;

    /// <summary>Gets all current field, source, and revision issues shown below the actions.</summary>
    /// <remarks>
    /// This is the complete list for the user. The generate buttons disable themselves from the same
    /// two building blocks, so a listed issue and a blocked button can no longer contradict each other.
    /// Only the MudBlazor field messages stay out of that gate: they arrive one validation pass late,
    /// which would make the buttons flicker, and the validators behind them are evaluated directly by
    /// FieldIssues anyway.
    /// </remarks>
    private IReadOnlyList<string> ValidationIssues
    {
        get
        {
            List<string> issues = [.. this.formIssues, .. this.FieldIssues, .. this.SourceIssues];

            if (this.selectedBriefing is { Versions.Count: > 0 } && !this.SelectedVersionSupportsEdits)
                issues.Add(T("This version has no compatible semantic artifacts. Rebuild the briefing instead."));

            return [.. issues.Where(issue => !string.IsNullOrWhiteSpace(issue)).Distinct(StringComparer.Ordinal)];
        }
    }

    /// <summary>Gets the field issues that block generation regardless of the edit mode.</summary>
    private IReadOnlyList<string> FieldIssues
    {
        get
        {
            List<string> issues = [];

            AddIssue(issues, this.ValidateProjectName(this.editor.Name));
            AddIssue(issues, this.ValidateProvider(this.editor.Provider));
            AddIssue(issues, this.ValidateCustomTargetLanguage(this.editor.CustomTargetLanguage));
            AddIssue(issues, this.ValidateCustomProtectionLevel(this.editor.CustomProtectionLevel));

            return issues;
        }
    }

    /// <summary>Gets the issues with the stored sources, which block only the modes that read them.</summary>
    /// <remarks>
    /// The image check belongs here rather than to the fields, even though it depends on the selected
    /// model: it only matters for the modes that hand the sources to the model at all. Changing just the
    /// design reuses the stored evidence and sends no attachments, which is the same distinction the
    /// build orchestrator makes before it runs source preparation.
    /// </remarks>
    private IReadOnlyList<string> SourceIssues
    {
        get
        {
            if (this.selectedBriefing is null)
                return [];

            List<string> issues = [];
            if (!this.HasSourceMaterial)
                issues.Add(T("Please add at least one source material file."));

            // A model can be selected long after the images were attached, so the capability that was
            // checked while attaching them has to be checked again here:
            if (this.HasImageSources && this.editor.Provider != ProviderSettings.NONE && !this.editor.Provider.SupportsImageInput())
                issues.Add(T("Images are not supported by the selected provider and model. Select a model with image support, or remove the image sources."));

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

            return issues;
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
        this.editor.TargetLanguage is CommonLanguages.OTHER && string.IsNullOrWhiteSpace(language)
            ? T("Please provide a custom target language.")
            : null;

    /// <summary>Validates the free-form protection level when Other is selected.</summary>
    private string? ValidateCustomProtectionLevel(string level) =>
        this.editor.ProtectionLevel is VisualBriefingProtectionLevel.OTHER && string.IsNullOrWhiteSpace(level)
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