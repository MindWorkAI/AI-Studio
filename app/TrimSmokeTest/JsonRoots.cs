using System.Text.Json;

using AIStudio.Agents;
using AIStudio.Agents.AssistantAudit;
using AIStudio.Assistants.Builder;
using AIStudio.Assistants.PromptOptimizer;
using AIStudio.Assistants.SWOTAnalysis;
using AIStudio.Assistants.VisualBriefing;
using AIStudio.Chat;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Settings.DataModel.PreviousModels;
using AIStudio.Tools;
using AIStudio.Tools.DataBackup;

namespace AIStudio.TrimSmokeTest;

/// <summary>
/// The types which AI Studio reads or writes as JSON on their own, not only as part of another type.
/// </summary>
/// <remarks>
/// Whatever these types hold, i.e., the types of their properties, the elements of their collections, and
/// their derived types, gets checked along with them, so only the outermost types belong here. Add a type
/// whenever AI Studio starts to store a new kind of file or to read a new kind of model answer.<br/><br/>
///
/// The answers of models are read with options of their own, which are private and only change names and
/// leniency. The default options build the same contracts for the types of .NET they hold. A type which is
/// private to its parser, such as the DTO of a slide deck, is out of reach here.
/// </remarks>
public static class JsonRoots
{
    public static readonly IReadOnlyList<JsonRoot> ALL =
    [
        // Chats, in the workspaces, among the temporary chats, and in chat archives:
        new(typeof(ChatThread), WorkspaceBehaviour.JSON_OPTIONS),

        // The settings, and their older versions, which the migrations still read:
        new(typeof(Data), SettingsManager.JSON_OPTIONS),
        new(typeof(DataV1V3), SettingsManager.JSON_OPTIONS),
        new(typeof(DataV4), SettingsManager.JSON_OPTIONS),
        new(typeof(DataV5), SettingsManager.JSON_OPTIONS),

        // The projects of the Visual Briefing assistant:
        new(typeof(VisualBriefingManifest), VisualBriefingJson.Persistence),
        new(typeof(VisualBriefingBuildRecord), VisualBriefingJson.Persistence),
        new(typeof(VisualBriefingEvidenceArtifact), VisualBriefingJson.Persistence),
        new(typeof(VisualBriefingPlanArtifact), VisualBriefingJson.Persistence),
        new(typeof(VisualBriefingContentArtifact), VisualBriefingJson.Persistence),
        new(typeof(VisualBriefingPresentationArtifact), VisualBriefingJson.Persistence),

        // Backups and their restore:
        new(typeof(DataBackupManifest), DataBackupFormat.JSON_OPTIONS),
        new(typeof(DataRestorePlan), DataBackupFormat.JSON_OPTIONS),
        new(typeof(DataRestoreReport), DataBackupFormat.JSON_OPTIONS),

        // Answers of models:
        new(typeof(LuaResponse), JsonSerializerOptions.Default),
        new(typeof(LauncherTextsResponse), JsonSerializerOptions.Default),
        new(typeof(PromptOptimizationResult), JsonSerializerOptions.Default),
        new(typeof(RetrievalContextValidationResult), JsonSerializerOptions.Default),
        new(typeof(AssistantAuditResult), JsonSerializerOptions.Default),
        new(typeof(SwotCategoryAnalysisResult), JsonSerializerOptions.Default),
        new(typeof(SwotModerationResult), JsonSerializerOptions.Default),
    ];
}