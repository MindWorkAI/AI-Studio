using System.Text.Json;

using AIStudio.Assistants.VisualBriefing;

namespace AIStudio.Tests.Serialization;

/// <summary>
/// A made-up visual briefing for the tests which check the JSON the Visual Briefing assistant compiles.
/// </summary>
/// <remarks>
/// The briefing holds one chart of every kind and a simulation with controls and a formula, so that every shape
/// of the compiled data occurs. Its layout holds no components: the data of a briefing comes from its content
/// alone.
/// </remarks>
internal static class VisualBriefingSamples
{
    private static readonly VisualBriefingChartKind[] CHART_KINDS = Enum.GetValues<VisualBriefingChartKind>();

    internal static VisualBriefingPlanArtifact Plan() => new()
    {
        Sections =
        [
            new()
            {
                SectionId = "overview",
                Role = VisualBriefingSectionRole.HERO,
                TitleSlotId = "overview-title",
                SummarySlotId = "overview-summary",
                Components =
                [
                    .. CHART_KINDS.Select(kind => new VisualBriefingPlanComponent
                    {
                        ComponentId = ChartId(kind),
                        Kind = VisualBriefingComponentKind.CHART,
                        EvidenceIds = ["F1"],
                        Slots =
                        [
                            new() { SlotId = $"{ChartId(kind)}-title", Role = VisualBriefingSlotRole.TITLE },
                            new() { SlotId = $"{ChartId(kind)}-caption", Role = VisualBriefingSlotRole.CAPTION },
                        ],
                    }),

                    new()
                    {
                        ComponentId = "simulation",
                        Kind = VisualBriefingComponentKind.SIMULATION,
                        EvidenceIds = ["M1"],
                        Slots =
                        [
                            new() { SlotId = "simulation-title", Role = VisualBriefingSlotRole.TITLE },
                            new() { SlotId = "simulation-summary", Role = VisualBriefingSlotRole.SUMMARY },
                            new() { SlotId = "simulation-result", Role = VisualBriefingSlotRole.RESULT },
                        ],
                    },
                ],
            },
        ],
    };

    internal static VisualBriefingContentArtifact Content()
    {
        var plan = Plan();
        var slotIds = plan.Sections.SelectMany(section => new[] { section.TitleSlotId, section.SummarySlotId }
            .Concat(section.Components.SelectMany(component => component.Slots.Select(slot => slot.SlotId))));

        return new()
        {
            Slots = [.. slotIds.Select(slotId => new VisualBriefingSlotValue { SlotId = slotId, Value = Json($"\"Text of {slotId}, with Größe and €\"") })],
            Charts =
            [
                .. CHART_KINDS.Select(kind => new VisualBriefingChartSpec
                {
                    ComponentId = ChartId(kind),
                    Kind = kind,
                    Categories = ["Q1", "Q2", "Q3"],
                    Series =
                    [
                        new() { Name = "Revenue", Values = [12.5m, 14m, 9.75m] },
                        new() { Name = "Costs", Values = [8m, 7.25m, 10m] },
                    ],
                }),
            ],

            Controls =
            [
                new()
                {
                    ControlId = "growth",
                    ComponentId = "simulation",
                    Kind = VisualBriefingControlKind.RANGE,
                    InitialValue = Json("3"),
                },

                new()
                {
                    ControlId = "scenario",
                    ComponentId = "simulation",
                    Kind = VisualBriefingControlKind.SELECT,
                    InitialValue = Json("\"base\""),
                    Options =
                    [
                        new() { Value = "base", Label = "Base case" },
                        new() { Value = "high", Label = "Größeres Wachstum" },
                    ],
                },
            ],

            Formulas =
            [
                new()
                {
                    ComponentId = "simulation",
                    OutputSlotId = "simulation-result",
                    Formula = new()
                    {
                        FormulaVersion = 1,
                        Operation = "multiply",
                        Arguments =
                        [
                            new() { Path = "interactions.state.growth" },
                            new() { Value = Json("2") },
                        ],
                    },
                },
            ],

            AccessibilityTexts = new(StringComparer.Ordinal)
            {
                [ChartId(VisualBriefingChartKind.LINE)] = "Revenue rises in Q2 and falls in Q3.",
                ["simulation"] = "The growth changes the result.",
            },

            SourceReferences = new(StringComparer.Ordinal)
            {
                [ChartId(VisualBriefingChartKind.LINE)] = ["S1", "S2"],
            },

            ResetLabel = "Zurücksetzen",
        };
    }

    internal static VisualBriefingLayoutNode Layout() => new()
    {
        NodeId = "root",
        Kind = VisualBriefingLayoutNodeKind.STACK,
    };

    /// <summary>
    /// The prepared sources of a briefing, with a transcript for its first source.
    /// </summary>
    internal static VisualBriefingPreparedSources PreparedSources(VisualBriefingManifest manifest) => new()
    {
        Transcripts = new Dictionary<Guid, string>
        {
            [manifest.Sources[0].SourceId] = "The transcript of a recording, with Größe and €.",
        },
    };

    /// <summary>
    /// A new revision of a briefing, with an embedded picture and two models which contributed to it.
    /// </summary>
    internal static VisualBriefingRevisionRequest RevisionRequest(VisualBriefingManifest manifest) => new(
        BriefingId: manifest.BriefingId,
        ParentRevisionId: null,
        EditMode: VisualBriefingEditMode.INITIAL,
        Instruction: "Summarize the quarter.",
        Data: Json("""{"slots":{"title":"Größe"},"assets":{"old":"x"},"footerTemplates":{},"protectionLabel":"x","_mwai":{}}"""),
        TemplateHtml: "<main></main>",
        Css: "main {}",
        ModelDisplayName: "Model A",
        Origin: "test",
        ModelContributions:
        [
            new(VisualBriefingModelRole.CONTENT, "Model A"),
            new(VisualBriefingModelRole.DESIGN, "Model B"),
        ],
        CreatedAtUtc: new DateTimeOffset(2026, 11, 3, 9, 30, 0, TimeSpan.Zero),
        EmbeddedAssets: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["asset-1"] = "data:image/png;base64,AA==",
        },
        AssetPlan:
        [
            new() { AssetId = "asset-1", Description = "A chart of the quarter", AltText = "Revenue per quarter, with Größe" },
        ]);

    private static string ChartId(VisualBriefingChartKind kind) => $"chart-{kind.ToString().ToLowerInvariant()}";

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}