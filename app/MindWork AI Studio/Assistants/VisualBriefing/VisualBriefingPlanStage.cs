using System.Text.Json;

using AIStudio.Settings;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Produces an immutable validated semantic plan from the evidence artifact.
/// </summary>
/// <param name="stageRunner">The structured model-stage runner.</param>
/// <param name="store">The persistent visual briefing store.</param>
/// <param name="progressService">The live build progress service.</param>
internal sealed class VisualBriefingPlanStage(StructuredLlmStageRunner stageRunner, VisualBriefingStore store, VisualBriefingBuildProgressService progressService)
{
    /// <summary>
    /// Produces or resumes the immutable plan artifact for one build.
    /// </summary>
    /// <param name="manifest">The briefing manifest.</param>
    /// <param name="provider">The selected provider and model.</param>
    /// <param name="profile">The selected prompt profile.</param>
    /// <param name="evidence">The validated evidence artifact.</param>
    /// <param name="build">The persistent build record.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The validated immutable plan artifact.</returns>
    public async Task<VisualBriefingPlanArtifact> ExecuteAsync(VisualBriefingManifest manifest, ProviderSettings provider, Profile profile, VisualBriefingEvidenceArtifact evidence, VisualBriefingBuildRecord build, CancellationToken token)
    {
        if (build.PlanArtifactId is { } completedId)
        {
            var completed = await store.ReadPlanArtifactAsync(manifest.BriefingId, completedId, token);
            if (completed is not null)
                return completed;
        }
        
        var stage = VisualBriefingEvidenceStage.Start(build, VisualBriefingBuildStage.PLAN, VisualBriefingHashing.ComputeSections(evidence.PayloadHash,
                VisualBriefingHashing.Compute(manifest.Settings.Instruction), manifest.Settings.AudienceProfile.ToString(),
                manifest.Settings.AudienceAgeGroup.ToString(), manifest.Settings.AudienceOrganizationalLevel.ToString(),
                manifest.Settings.AudienceExpertise.ToString(), provider.Id, provider.Model.Id, profile.Id,
                VisualBriefingHashing.Compute(profile.ToSystemPrompt()), VisualBriefingVersions.PLAN_CONTRACT.ToString()));
        
        await store.SaveBuildAsync(build, token);
        progressService.Publish(build);
        
        var run = await stageRunner.RunAsync<VisualBriefingPlanResponse>(provider, profile, BuildSystemContract(), BuildPrompt(manifest, evidence),
            [], VisualBriefingBuildStage.PLAN, build.OperationId, build.BuildId, response => VisualBriefingValidation.ValidatePlan(evidence, response), token);
        
        stage.Attempts = run.Attempts;
        if (!run.Success || run.Response is null)
            await VisualBriefingEvidenceStage.FailAsync(store, build, stage, run, VisualBriefingValidationRule.REFERENCE_INVALID, token);

        var sections = run.Response!.Sections;
        var structuralSignature = VisualBriefingHashing.Compute(string.Join('\u001f', sections.Select(section => $"{section.SectionId}:{section.Role}:{section.TitleSlotId}:{section.SummarySlotId}")
            .Concat(sections.SelectMany(section => section.Components)
                .Select(component =>
                    $"{component.ComponentId}:{component.Kind}:{component.AssetId}:{component.TimelineOrientation}:{string.Join(',', component.Slots.Select(slot => $"{slot.SlotId}:{slot.Role}"))}"))));

        var artifact = new VisualBriefingPlanArtifact
        {
            ArtifactId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = VisualBriefingPayloadHash.ForPlan(sections, structuralSignature),
            Sections = sections,
            StructuralSignature = structuralSignature,
            Model = VisualBriefingModelNames.ExportLabel(provider),
        };

        await store.WritePlanArtifactAsync(manifest.BriefingId, artifact, token);
        build.PlanArtifactId = artifact.ArtifactId;
        VisualBriefingEvidenceStage.Complete(build, stage, artifact.PayloadHash);

        await store.SaveBuildAsync(build, token);
        progressService.Publish(build);

        return artifact;
    }

    private static string BuildSystemContract() =>
        $$"""
          You are the Planning Agent for the Visual Briefing Assistant in MindWork AI Studio.
          Return exactly one JSON object without Markdown or commentary. Unknown fields are forbidden.
          Never return HTML, CSS, JavaScript, ECharts options, data-mwai attributes, visual layout, design tokens, or content values.
          The object has exactly contractVersion={{VisualBriefingVersions.PLAN_CONTRACT}} and ordered sections.
          Each section has exactly sectionId, role, titleSlotId, summarySlotId, and components.
          Every section contains at least one component.
          Section roles are HERO, EXECUTIVE_SUMMARY, NARRATIVE, EVIDENCE, EXPLORATION, or CONCLUSION.
          The first section is the only HERO. EXECUTIVE_SUMMARY may occur once directly after it. CONCLUSION may occur once as the final section.
          Every titleSlotId and summarySlotId is a unique content slot ID.
          Each component has exactly componentId, kind, evidenceIds, slots, assetId, and timelineOrientation.
          Every slot has exactly slotId and role. Slot roles are EYEBROW, TITLE, SUMMARY, BODY, LABEL, VALUE, CONTEXT, CAPTION, TABLE_DATA, PANEL, RESULT, or TIMELINE_DATA.
          Allowed kinds: TEXT, METRIC, TABLE, CHART, ASSET, CALLOUT, TABS, ACCORDION, FILTERABLE_TABLE, SIMULATION, TIMELINE.
          IDs are stable lowercase identifiers matching ^[a-z][a-z0-9_-]{0,63}$. Reference only supplied evidence IDs.
          Slot IDs are unique across the whole briefing, including section title and summary slots.
          Use these exact component slot patterns:
          TEXT: TITLE, BODY.
          METRIC: LABEL, VALUE, CONTEXT.
          CALLOUT: EYEBROW, TITLE, BODY.
          CHART and ASSET: TITLE, CAPTION.
          TABLE and FILTERABLE_TABLE: TITLE, SUMMARY, TABLE_DATA.
          TABS: TITLE, SUMMARY, then one or more PANEL slots.
          ACCORDION: TITLE, BODY.
          SIMULATION: TITLE, SUMMARY, then one or more RESULT slots.
          TIMELINE: TITLE, SUMMARY, TIMELINE_DATA.
          assetId is null except for ASSET components; include every supplied assetId in exactly one ASSET component.
          timelineOrientation is null except for TIMELINE components, where it is HORIZONTAL or VERTICAL.
          Use TIMELINE for sourced events, milestones, phases, or historical developments whose sequence matters; use CHART instead for quantitative trends over time.
          Choose HORIZONTAL for a concise overview with few milestones and VERTICAL for longer or explanation-rich chronological narratives.
          """;

    private static string BuildPrompt(VisualBriefingManifest manifest, VisualBriefingEvidenceArtifact evidence) =>
        $"""
         Audience: {manifest.Settings.AudienceProfile}; {manifest.Settings.AudienceAgeGroup}; {manifest.Settings.AudienceOrganizationalLevel}; {manifest.Settings.AudienceExpertise}
         Scope instruction: {manifest.Settings.Instruction}
         Evidence: {JsonSerializer.Serialize(new { evidence.Facts, evidence.Metrics, evidence.Tables, evidence.AssetPlan }, VisualBriefingJson.Canonical)}
         """;
}