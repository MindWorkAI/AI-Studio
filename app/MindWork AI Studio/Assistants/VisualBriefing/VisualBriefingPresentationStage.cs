using System.Text.Json;

using AIStudio.Settings;

using ProviderSettings = AIStudio.Settings.Provider;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Produces only a layout DSL and bounded tokens, then dry-runs deterministic compilation.
/// </summary>
internal sealed class VisualBriefingPresentationStage(StructuredLlmStageRunner stageRunner, VisualBriefingStore store, VisualBriefingBuildProgressService progressService, ILogger<VisualBriefingPresentationStage> logger)
{
    public async Task<VisualBriefingPresentationArtifact> ExecuteAsync(VisualBriefingManifest manifest, ProviderSettings provider, Profile profile,
        VisualBriefingPlanArtifact plan, VisualBriefingContentArtifact content, VisualBriefingPresentationArtifact? parentPresentation,
        VisualBriefingBuildRecord build, CancellationToken token)
    {
        if (build.PresentationArtifactId is { } completedId)
        {
            var completed = await store.ReadPresentationArtifactAsync(manifest.BriefingId, completedId, token);
            if (completed is not null)
                return completed;
        }

        var stage = GetStage(build, VisualBriefingBuildStage.DESIGN);
        stage.Status = VisualBriefingBuildStageStatus.RUNNING;
        stage.StartedAtUtc = DateTimeOffset.UtcNow;
        stage.FinishedAtUtc = null;
        stage.Failure = null;
        stage.InputFingerprint = VisualBriefingHashing.ComputeSections(
            plan.PayloadHash,
            content.PayloadHash,
            VisualBriefingHashing.Compute(manifest.Settings.Instruction),
            parentPresentation?.PayloadHash ?? string.Empty,
            provider.Id,
            provider.Model.Id,
            profile.Id,
            VisualBriefingHashing.Compute(profile.ToSystemPrompt()),
            VisualBriefingVersions.DESIGN_CONTRACT.ToString());
        
        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await store.SaveBuildAsync(build, token);
        progressService.Publish(build);

        var run = await stageRunner.RunAsync<VisualBriefingDesignResponse>(provider, profile, BuildSystemContract(),
            BuildPrompt(manifest, plan, parentPresentation), [], VisualBriefingBuildStage.DESIGN, build.OperationId, build.BuildId,
            response => ValidateDesign(manifest, plan, content, response), token);
        
        stage.Attempts = run.Attempts;
        if (!run.Success || run.Response is null)
        {
            var failure = new VisualBriefingFailure
            {
                Code = run.FailureCode,
                Stage = VisualBriefingBuildStage.DESIGN,
                
                ValidationRule = run.ValidationRule is VisualBriefingValidationRule.NONE
                    ? VisualBriefingValidationRule.LAYOUT_INVALID
                    : run.ValidationRule,
                
                UserMessage = run.Issue,
                
                TechnicalDetails = run.Diagnostic is null
                    ? $"Rule={(run.ValidationRule is VisualBriefingValidationRule.NONE ? VisualBriefingValidationRule.LAYOUT_INVALID : run.ValidationRule)}; Attempts={run.Attempts}; ResponseLength={run.ResponseLength}."
                    : $"Rule={(run.ValidationRule is VisualBriefingValidationRule.NONE ? VisualBriefingValidationRule.LAYOUT_INVALID : run.ValidationRule)}; Attempts={run.Attempts}; ResponseLength={run.ResponseLength}; {run.Diagnostic.ToTechnicalDetails()}.",
                
                StructuredResponse = run.Diagnostic,
            };
            
            stage.Status = VisualBriefingBuildStageStatus.FAILED;
            stage.FinishedAtUtc = DateTimeOffset.UtcNow;
            stage.Failure = failure;
            
            build.Status = VisualBriefingBuildStatus.FAILED;
            build.Failure = failure;
            build.UpdatedAtUtc = DateTimeOffset.UtcNow;
            
            await store.SaveBuildAsync(build, token);
            throw new VisualBriefingBuildException(failure.Code, failure.Stage, failure.UserMessage, failure.TechnicalDetails);
        }

        var compiled = VisualBriefingLayoutCompiler.Compile(plan, content, run.Response.Layout, run.Response.Profile);
        var payloadHash = VisualBriefingPayloadHash.ForPresentation(run.Response.Layout, run.Response.Profile, compiled.TemplateHash, compiled.CssHash);
        
        var artifact = new VisualBriefingPresentationArtifact
        {
            ArtifactId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PayloadHash = payloadHash,
            Layout = run.Response.Layout,
            Profile = run.Response.Profile,
            TemplateHtml = compiled.TemplateHtml,
            Css = compiled.Css,
            TemplateHash = compiled.TemplateHash,
            CssHash = compiled.CssHash,
            Model = VisualBriefingModelNames.ExportLabel(provider),
        };
        
        await store.WritePresentationArtifactAsync(manifest.BriefingId, artifact, token);
        build.PresentationArtifactId = artifact.ArtifactId;
        
        stage.Status = VisualBriefingBuildStageStatus.COMPLETED;
        stage.FinishedAtUtc = DateTimeOffset.UtcNow;
        stage.OutputHash = artifact.PayloadHash;
        stage.Failure = null;
        
        build.Status = VisualBriefingBuildStatus.ACTIVE;
        build.Failure = null;
        build.UpdatedAtUtc = DateTimeOffset.UtcNow;
        
        await store.SaveBuildAsync(build, token);
        progressService.Publish(build);
        logger.LogInformation("Visual briefing design completed. OperationId={OperationId} BuildId={BuildId} LayoutHash={LayoutHash} TemplateHash={TemplateHash} CssHash={CssHash}", build.OperationId, build.BuildId, VisualBriefingHashing.Compute(JsonSerializer.Serialize(artifact.Layout, VisualBriefingJson.Canonical)), artifact.TemplateHash, artifact.CssHash);
        
        return artifact;
    }

    private static VisualBriefingContractIssue? ValidateDesign(VisualBriefingManifest manifest, VisualBriefingPlanArtifact plan, VisualBriefingContentArtifact content, VisualBriefingDesignResponse response)
    {
        var issue = VisualBriefingValidation.ValidateDesign(plan, response);
        if (issue is not null)
            return issue;

        // The layout has been validated above, so the compilation below only guards AI Studio's own
        // compiler output, see VisualBriefingCompilerInvariant:
        var compiled = VisualBriefingCompilerInvariant.Guard(VisualBriefingBuildStage.DESIGN,
            () => VisualBriefingLayoutCompiler.Compile(plan, content, response.Layout, response.Profile));
        
        var data = compiled.Data.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        data["_mwai"] = JsonSerializer.SerializeToElement(new
        {
            schemaVersion = VisualBriefingVersions.SCHEMA,
            runtimeVersion = VisualBriefingVersions.RUNTIME,
            aiStudioVersion = "validation",
            
            assets = content.AssetPlan.ToDictionary(
                asset => asset.AssetId,
                _ => "data:image/png;base64,AA==",
                StringComparer.Ordinal),
            
            footer = new
            {
                createdWith = "validation",
                models = "validation",
                createdAt = "validation",
                authors = "validation",
                protection = "validation",
            },
        }, VisualBriefingJson.Canonical);
        
        var validationData = JsonSerializer.SerializeToElement(data, VisualBriefingJson.Canonical);
        VisualBriefingCompilerInvariant.Guard(VisualBriefingBuildStage.DESIGN,
            VisualBriefingArtifactService.ValidateGeneratedParts(manifest, validationData, compiled.TemplateHtml, compiled.Css, content.Charts.Count > 0));
        
        return null;
    }

    private static string BuildSystemContract() =>
        $"""
          You are the Design Agent for the Visual Briefing Assistant in MindWork AI Studio.
          Return exactly one JSON object without Markdown or commentary. Unknown fields are forbidden.
          You may only compose the supplied component IDs into the layout DSL and select bounded design tokens.
          Never return HTML, CSS, ECharts options, data-mwai attributes, JavaScript, URLs, or executable text.

          The object has exactly:
          - "contractVersion": {VisualBriefingVersions.DESIGN_CONTRACT}
          - "profile": EDITORIAL for narrative storytelling, EXECUTIVE for concise decision briefings,
            or ANALYTICAL for dense evidence and data.
          - "layout": a recursive node with exactly nodeId, kind (SECTION, STACK, GRID, COMPONENT),
            sectionId (the planned section ID for SECTION, otherwise null),
            componentId (the planned component ID for COMPONENT, otherwise null),
            children, columns (mobile/tablet/desktop for GRID, otherwise null),
            span (1..12), order (0..1000), emphasized, and alignment (START, CENTER, END, STRETCH).
            Every nodeId is a unique lowercase identifier and must differ from every section and component ID.

          The layout root is one STACK. Its direct children are one SECTION for every planned section,
          in plan order, with the matching sectionId. A section may contain STACK and GRID containers,
          and must reference exactly its own components. Reference every supplied component exactly once.
          Give a HORIZONTAL TIMELINE enough width for its ordered track; do not place it in a narrow grid column.
          Prefer editorial rhythm over a wall of cards. Use emphasis sparingly for decisive metrics or insights.
          MindWork AI Studio owns all colors, typography, surfaces, and chart styling.
          """;

    private static string BuildPrompt(VisualBriefingManifest manifest, VisualBriefingPlanArtifact plan, VisualBriefingPresentationArtifact? parent)
    {
        var parentJson = parent is null ? "none" : JsonSerializer.Serialize(new { parent.Layout, parent.Profile }, VisualBriefingJson.Canonical);
        return $"""
                Operation: {(parent is null ? "CREATE_DESIGN" : "CHANGE_DESIGN")}
                Design instruction: {manifest.Settings.Instruction}
                Planned sections and components:
                {JsonSerializer.Serialize(plan.Sections, VisualBriefingJson.Canonical)}
                Parent design:
                {parentJson}
                """;
    }

    private static VisualBriefingBuildStageRecord GetStage(VisualBriefingBuildRecord build, VisualBriefingBuildStage stage)
    {
        var record = build.Stages.FirstOrDefault(candidate => candidate.Stage == stage);
        if (record is not null)
            return record;
        
        record = new() { Stage = stage };
        build.Stages.Add(record);
        
        return record;
    }
}