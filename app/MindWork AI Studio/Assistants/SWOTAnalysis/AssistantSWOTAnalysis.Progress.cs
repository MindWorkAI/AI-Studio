using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AIStudio.Assistants.SWOTAnalysis;

/// <summary>
/// The part of the SWOT analysis assistant which shows what is going on while the analysis runs.
/// </summary>
/// <remarks>
/// None of the answers is shown while it streams: they are JSON, and they come from private threads.
/// The progress text below the progress bar names the stage, and the categories show their findings
/// as soon as each one is ready.
/// </remarks>
public partial class AssistantSWOTAnalysis
{
    protected override bool ShowDedicatedProgress => true;

    protected override string ProgressText =>
        this.isGatheringSharedResearch ? T("Gathering shared research...") :
        this.isFinalizingAnalysis ? T("Creating prioritized actions...") :
        this.categoryAnalysisProgress.Values.Any(progress => progress.Status is SwotCategoryAnalysisStatus.RUNNING) ? T("Analyzing the four SWOT categories...") :
        string.Empty;

    private protected override RenderFragment? BelowSubmitContent =>
        this.categoryAnalysisProgress.Count > 0 && this.analysisResult is null
            ? new RenderFragment(this.RenderProgress)
            : null;

    private void RenderProgress(RenderTreeBuilder builder)
    {
        builder.OpenComponent<SwotAnalysisProgressView>(0);
        builder.AddComponentParameter(1, nameof(SwotAnalysisProgressView.Progress), this.categoryAnalysisProgress);
        builder.CloseComponent();
    }
}