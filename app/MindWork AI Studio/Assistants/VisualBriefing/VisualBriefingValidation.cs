using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Validates the structured responses of the four model stages against their contracts.
/// </summary>
/// <remarks>
/// Every rule here describes something the model can actually correct, reported with a JSON path and
/// an expected shape so the repair turn has something to act on. Failures of AI Studio's own
/// compiler are not contract violations and are handled by <see cref="VisualBriefingCompilerInvariant"/>.
/// </remarks>
internal static partial class VisualBriefingValidation
{
    private const int MAX_OPTION_VALUE_LENGTH = 128;

    private static readonly Regex ID = IdRegex();

    /// <summary>
    /// Lists tokens that never occur in ordinary target-language prose. Broader patterns such as a
    /// bare "document." or "=>" are deliberately absent: they reject normal sentences, and model text
    /// only ever reaches the artifact as text content.
    /// </summary>
    private static readonly string[] FORBIDDEN_MODEL_TEXT =
    [
        "data-mwai-", "javascript:", "echarts", "function(",
    ];

    internal static VisualBriefingContractIssue? ValidateEvidence(
        VisualBriefingManifest manifest,
        VisualBriefingEvidenceResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.EVIDENCE_CONTRACT)
            return Invalid(
                "The evidence response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED,
                "$.contractVersion",
                expected: "supported contract version");
        var evidenceIdLocations = response.Facts
            .Select((item, index) => (item.EvidenceId, Path: $"$.facts[{index}].evidenceId"))
            .Concat(response.Metrics
                .Select((item, index) => (item.EvidenceId, Path: $"$.metrics[{index}].evidenceId")))
            .Concat(response.Tables
                .Select((item, index) => (item.EvidenceId, Path: $"$.tables[{index}].evidenceId")))
            .ToArray();
        var invalidEvidenceId = FindInvalidOrDuplicateId(evidenceIdLocations);
        if (invalidEvidenceId is not null)
            return Invalid(
                "Evidence IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidEvidenceId,
                "evidenceId",
                "unique lowercase ID");
        var sourceIds = VisualBriefingSourceHandles.Map(manifest)
            .Select(item => item.Handle)
            .ToHashSet(StringComparer.Ordinal);
        if (response.SourceCoverage.Count != sourceIds.Count ||
            response.SourceCoverage.Select(item => item.SourceId).Distinct().Count() != sourceIds.Count ||
            response.SourceCoverage.Any(item =>
                !sourceIds.Contains(item.SourceId) ||
                string.IsNullOrWhiteSpace(item.Reason)))
            return new(
                VisualBriefingFailureCode.SOURCE_COVERAGE_INVALID,
                "Source coverage must contain every source exactly once.",
                VisualBriefingValidationRule.SOURCE_COVERAGE_INVALID);
        if (response.Facts.Any(item =>
                item.SourceIds.Count == 0 ||
                item.SourceIds.Distinct().Count() != item.SourceIds.Count ||
                item.SourceIds.Any(id => !sourceIds.Contains(id))) ||
            response.Metrics.Any(item =>
                item.SourceIds.Count == 0 ||
                item.SourceIds.Distinct().Count() != item.SourceIds.Count ||
                item.SourceIds.Any(id => !sourceIds.Contains(id))) ||
            response.Tables.Any(item =>
                item.SourceIds.Count == 0 ||
                item.SourceIds.Distinct().Count() != item.SourceIds.Count ||
                item.SourceIds.Any(id => !sourceIds.Contains(id)) ||
                item.Columns.Count == 0 ||
                item.Rows.Any(row => row.Count != item.Columns.Count)) ||
            response.Facts.Any(item => string.IsNullOrWhiteSpace(item.Statement)) ||
            response.Metrics.Any(item => string.IsNullOrWhiteSpace(item.Label)) ||
            response.Tables.Any(item => string.IsNullOrWhiteSpace(item.Title)))
            return Invalid(
                "Every evidence item must reference a supplied source.",
                VisualBriefingValidationRule.REFERENCE_INVALID);
        var assetIds = manifest.Sources
            .Where(source => source.Kind is VisualBriefingSourceKind.VISUAL_ASSET)
            .Select(source => source.AssetId)
            .ToHashSet(StringComparer.Ordinal);
        if (response.AssetPlan.Count != assetIds.Count ||
            response.AssetPlan.Select(item => item.AssetId).Distinct(StringComparer.Ordinal).Count() != assetIds.Count ||
            response.AssetPlan.Any(item =>
                !assetIds.Contains(item.AssetId) ||
                string.IsNullOrWhiteSpace(item.Description) ||
                string.IsNullOrWhiteSpace(item.AltText)))
            return new(
                VisualBriefingFailureCode.ASSET_PLAN_INVALID,
                "The asset plan must contain every visual asset exactly once.",
                VisualBriefingValidationRule.ASSET_PLAN_INVALID);
        return ContainsForbidden(response)
            ? Invalid(
                "Evidence must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    internal static VisualBriefingContractIssue? ValidatePlan(VisualBriefingEvidenceArtifact evidence, VisualBriefingPlanResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.PLAN_CONTRACT)
            return Invalid(
                "The plan response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED,
                "$.contractVersion",
                expected: "supported contract version");
        
        var evidenceIds = evidence.Facts.Select(item => item.EvidenceId)
            .Concat(evidence.Metrics.Select(item => item.EvidenceId))
            .Concat(evidence.Tables.Select(item => item.EvidenceId))
            .ToHashSet(StringComparer.Ordinal);
        
        var components = response.Sections.SelectMany(item => item.Components).ToArray();
        if (response.Sections.Count == 0)
            return Invalid(
                "Plan section and component IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                "$.sections",
                expected: "non-empty section array");
        
        if (response.Sections.Any(section => section.Components.Count == 0))
            return Invalid(
                "Every plan section requires at least one component.",
                VisualBriefingValidationRule.REFERENCE_INVALID,
                "$.sections",
                expected: "one or more components per section");
        
        var invalidSectionId = FindInvalidOrDuplicateId(response.Sections
            .Select((section, sectionIndex) =>
                (section.SectionId, Path: $"$.sections[{sectionIndex}].sectionId")));
        
        if (invalidSectionId is not null)
            return Invalid(
                "Plan section and component IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidSectionId,
                "sectionId",
                "unique lowercase ID");
        
        var conclusionIndex = response.Sections.FindIndex(section => section.Role is VisualBriefingSectionRole.CONCLUSION);
        
        if (response.Sections[0].Role is not VisualBriefingSectionRole.HERO ||
            response.Sections.Skip(1).Any(section => section.Role is VisualBriefingSectionRole.HERO) ||
            response.Sections.Count(section => section.Role is VisualBriefingSectionRole.EXECUTIVE_SUMMARY) > 1 ||
            response.Sections.FindIndex(section => section.Role is VisualBriefingSectionRole.EXECUTIVE_SUMMARY) is > 1 ||
            response.Sections.Count(section => section.Role is VisualBriefingSectionRole.CONCLUSION) > 1 ||
            conclusionIndex >= 0 &&
            conclusionIndex != response.Sections.Count - 1)
            return Invalid(
                "The plan requires one opening hero and correctly positioned summary and conclusion sections.",
                VisualBriefingValidationRule.REFERENCE_INVALID,
                "$.sections",
                expected: "HERO first, optional EXECUTIVE_SUMMARY second, optional CONCLUSION last");
        
        var invalidComponentId = FindInvalidOrDuplicateId(response.Sections
            .SelectMany((section, sectionIndex) => section.Components
                .Select((component, componentIndex) =>
                    (component.ComponentId,
                        Path: $"$.sections[{sectionIndex}].components[{componentIndex}].componentId"))));
        
        if (invalidComponentId is not null)
            return Invalid(
                "Plan section and component IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidComponentId,
                "componentId",
                "unique lowercase ID");
        
        var invalidSlotId = FindInvalidOrDuplicateId(response.Sections
            .SelectMany((section, sectionIndex) =>
                new[]
                {
                    (section.TitleSlotId, Path: $"$.sections[{sectionIndex}].titleSlotId"),
                    (section.SummarySlotId, Path: $"$.sections[{sectionIndex}].summarySlotId"),
                }.Concat(section.Components.SelectMany((component, componentIndex) => component.Slots
                    .Select((slot, slotIndex) =>
                        (slot.SlotId,
                            Path: $"$.sections[{sectionIndex}].components[{componentIndex}].slots[{slotIndex}].slotId"))))));
        
        if (invalidSlotId is not null)
            return Invalid(
                "Plan slot IDs must be valid and unique.",
                VisualBriefingValidationRule.ID_INVALID,
                invalidSlotId,
                expected: "unique lowercase ID");
        
        if (components.Any(item =>
                item.EvidenceIds.Count == 0 ||
                item.EvidenceIds.Distinct(StringComparer.Ordinal).Count() != item.EvidenceIds.Count ||
                item.EvidenceIds.Any(id => !evidenceIds.Contains(id)) ||
                !HasValidSlotPattern(item) ||
                item.Kind is VisualBriefingComponentKind.TIMELINE &&
                item.TimelineOrientation is not (VisualBriefingTimelineOrientation.HORIZONTAL or VisualBriefingTimelineOrientation.VERTICAL) ||
                item.Kind is not VisualBriefingComponentKind.TIMELINE && item.TimelineOrientation is not null))
            return Invalid(
                "Every component must reference valid evidence and use the exact slots and orientation for its kind.",
                VisualBriefingValidationRule.REFERENCE_INVALID);
        
        var plannedAssetIds = components
            .Where(item => item.Kind is VisualBriefingComponentKind.ASSET)
            .Select(item => item.AssetId)
            .ToArray();
        
        var evidenceAssetIds = evidence.AssetPlan.Select(item => item.AssetId).ToHashSet(StringComparer.Ordinal);
        if (components.Any(item =>
                item.Kind is VisualBriefingComponentKind.ASSET && string.IsNullOrWhiteSpace(item.AssetId) ||
                item.Kind is not VisualBriefingComponentKind.ASSET && item.AssetId is not null) ||
            plannedAssetIds.Any(item => item is null) ||
            plannedAssetIds.Distinct(StringComparer.Ordinal).Count() != plannedAssetIds.Length ||
            !plannedAssetIds.Select(item => item!).ToHashSet(StringComparer.Ordinal).SetEquals(evidenceAssetIds) ||
            components.Where(item => item.Kind is not VisualBriefingComponentKind.ASSET)
                .Any(item => item.AssetId is not null))
            return Invalid(
                "The plan must include every visual asset exactly once.",
                VisualBriefingValidationRule.ASSET_PLAN_INVALID);
        
        return ContainsForbidden(response)
            ? Invalid(
                "The plan must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    internal static VisualBriefingContractIssue? ValidateContent(VisualBriefingPlanArtifact plan, VisualBriefingContentResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.CONTENT_CONTRACT)
            return Invalid(
                "The content response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED,
                "$.contractVersion",
                expected: "supported contract version");
        
        var components = plan.Sections.SelectMany(section => section.Components).ToArray();
        var componentById = components.ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        
        var chartComponentIds = components
            .Where(item => item.Kind is VisualBriefingComponentKind.CHART)
            .Select(item => item.ComponentId)
            .ToHashSet(StringComparer.Ordinal);
        
        var requiredSlots = plan.Sections
            .SelectMany(section => new[] { section.TitleSlotId, section.SummarySlotId }
                .Concat(section.Components.SelectMany(component => component.Slots.Select(slot => slot.SlotId))))
            .ToArray();
        
        var slots = response.Slots.Select(item => item.SlotId).ToArray();
        var duplicateSlotIndex = FindDuplicateIndex(slots);
        
        if (duplicateSlotIndex >= 0)
            return Invalid(
                "Every required content slot must be fulfilled exactly once.",
                VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                $"$.slots[{duplicateSlotIndex}].slotId",
                "slotId",
                "unique planned slot ID");
        
        var requiredSlotSet = requiredSlots.ToHashSet(StringComparer.Ordinal);
        var unknownSlotIndex = Array.FindIndex(slots, slotId => !requiredSlotSet.Contains(slotId));
        
        if (unknownSlotIndex >= 0)
            return Invalid(
                "Every required content slot must be fulfilled exactly once.",
                VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                $"$.slots[{unknownSlotIndex}].slotId",
                "slotId",
                "planned slot ID");
        
        if (slots.Length != requiredSlots.Length ||
            !slots.ToHashSet(StringComparer.Ordinal).SetEquals(requiredSlotSet))
            return Invalid(
                "Every required content slot must be fulfilled exactly once.",
                VisualBriefingValidationRule.SLOT_FULFILLMENT_INVALID,
                "$.slots",
                expected: "every planned slot exactly once");

        var slotTypes = VisualBriefingSlotTypes.Map(plan.Sections);
        for (var slotIndex = 0; slotIndex < response.Slots.Count; slotIndex++)
        {
            var slot = response.Slots[slotIndex];
            var slotType = slotTypes[slot.SlotId];
            var slotTypeIssue = VisualBriefingSlotTypes.Validate(slotType, slot.Value);
            if (!string.IsNullOrEmpty(slotTypeIssue))
                return Invalid(
                    slotTypeIssue,
                    VisualBriefingValidationRule.SLOT_VALUE_TYPE_INVALID,
                    $"$.slots[{slotIndex}].value",
                    "value",
                    VisualBriefingSlotTypes.Describe(slotType));

            // AI Studio derives the filter options of a filterable table from the first column and
            // compares them against the rendered cell text, so those cells must be text:
            var slotComponent = components.FirstOrDefault(item =>
                VisualBriefingSlotTypes.IsTableDataSlot(item, slot.SlotId) &&
                item.Kind is VisualBriefingComponentKind.FILTERABLE_TABLE);
            
            if (slotComponent is not null && !HasTextFirstColumn(slot.Value))
                return Invalid(
                    "The first column of a filterable table must contain text values.",
                    VisualBriefingValidationRule.SLOT_VALUE_TYPE_INVALID,
                    $"$.slots[{slotIndex}].value",
                    "value",
                    "string value in the first cell of every row");
        }

        HashSet<string> seenCharts = new(StringComparer.Ordinal);
        for (var chartIndex = 0; chartIndex < response.Charts.Count; chartIndex++)
        {
            var chart = response.Charts[chartIndex];
            if (!chartComponentIds.Contains(chart.ComponentId))
                return Invalid(
                    "A chart targets a component that is not a planned chart.",
                    VisualBriefingValidationRule.CHART_SET_INVALID,
                    $"$.charts[{chartIndex}].componentId",
                    "componentId",
                    "planned CHART component ID");
            
            if (!seenCharts.Add(chart.ComponentId))
                return Invalid(
                    "Every planned chart component requires exactly one chart.",
                    VisualBriefingValidationRule.CHART_SET_INVALID,
                    $"$.charts[{chartIndex}].componentId",
                    "componentId",
                    "unique planned CHART component ID");
            
            if (chart.Categories.Count == 0)
                return Invalid(
                    "Every chart requires categories.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].categories",
                    "categories",
                    "non-empty string array");
            
            var emptyCategoryIndex = chart.Categories.FindIndex(string.IsNullOrWhiteSpace);
            if (emptyCategoryIndex >= 0)
                return Invalid(
                    "Chart categories must be non-empty.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].categories[{emptyCategoryIndex}]",
                    expected: "non-empty string");
            
            if (chart.Series.Count == 0)
                return Invalid(
                    "Every chart requires at least one data series.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].series",
                    "series",
                    "non-empty series array");
            
            if (chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT &&
                chart.Series.Count != 1)
                return Invalid(
                    "Pie and donut charts require exactly one data series.",
                    VisualBriefingValidationRule.CHART_DATA_INVALID,
                    $"$.charts[{chartIndex}].series",
                    "series",
                    "exactly one series");
            
            for (var seriesIndex = 0; seriesIndex < chart.Series.Count; seriesIndex++)
            {
                var series = chart.Series[seriesIndex];
                if (string.IsNullOrWhiteSpace(series.Name))
                    return Invalid(
                        "Every chart series requires a name.",
                        VisualBriefingValidationRule.CHART_DATA_INVALID,
                        $"$.charts[{chartIndex}].series[{seriesIndex}].name",
                        "name",
                        "non-empty target-language string");
                
                if (series.Values.Count != chart.Categories.Count)
                    return Invalid(
                        "Every chart series requires one value per category.",
                        VisualBriefingValidationRule.CHART_DATA_INVALID,
                        $"$.charts[{chartIndex}].series[{seriesIndex}].values",
                        "values",
                        "one numeric value per category");
            }
        }
        
        if (!seenCharts.SetEquals(chartComponentIds))
            return Invalid(
                "Every planned chart component requires exactly one chart.",
                VisualBriefingValidationRule.CHART_SET_INVALID,
                "$.charts",
                expected: "exactly one chart for every planned CHART component");

        HashSet<string> seenControls = new(StringComparer.Ordinal);
        for (var controlIndex = 0; controlIndex < response.Controls.Count; controlIndex++)
        {
            var control = response.Controls[controlIndex];
            if (!IsUsableId(control.ControlId) || !seenControls.Add(control.ControlId))
                return Invalid(
                    "Control IDs must be valid and unique.",
                    VisualBriefingValidationRule.CONTROL_ID_INVALID,
                    $"$.controls[{controlIndex}].controlId",
                    "controlId",
                    "unique lowercase ID");
        
            if (!componentById.TryGetValue(control.ComponentId, out var component))
                return Invalid(
                    "A control targets an unknown component.",
                    VisualBriefingValidationRule.CONTROL_TARGET_INVALID,
                    $"$.controls[{controlIndex}].componentId",
                    "componentId",
                    "planned interactive component ID");
            
            if (!ControlMatchesComponent(control.Kind, component.Kind))
                return Invalid(
                    "A control kind is incompatible with its planned component.",
                    VisualBriefingValidationRule.CONTROL_TARGET_INVALID,
                    $"$.controls[{controlIndex}].kind",
                    "kind",
                    ExpectedControlKinds(component.Kind));
            
            var controlIssue = ValidateControlState(control, controlIndex);
            if (controlIssue is not null)
                return controlIssue;
        }

        foreach (var component in components)
        {
            var controls = response.Controls
                .Where(control => control.ComponentId == component.ComponentId)
                .ToArray();
            
            if (component.Kind is VisualBriefingComponentKind.TABS)
            {
                if (controls.Length != 1 || controls[0].Kind is not VisualBriefingControlKind.TAB)
                    return Invalid(
                        "Every tabs component requires exactly one TAB control.",
                        VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID,
                        "$.controls",
                        expected: "exactly one TAB control for every planned TABS component");
                
                if (controls[0].Options.Count != component.Slots.Count(slot => slot.Role is VisualBriefingSlotRole.PANEL))
                    return Invalid(
                        "Every tabs option requires one matching planned slot.",
                        VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID,
                        $"$.controls[{response.Controls.IndexOf(controls[0])}].options",
                        "options",
                        "one option per planned tab slot");
            }
            else if (component.Kind is VisualBriefingComponentKind.SIMULATION &&
                     controls.All(control =>
                         control.Kind is not (
                             VisualBriefingControlKind.NUMBER or
                             VisualBriefingControlKind.RANGE or
                             VisualBriefingControlKind.SELECT)))
                return Invalid(
                    "Every simulation requires at least one typed input control.",
                    VisualBriefingValidationRule.CONTROL_REQUIREMENT_INVALID,
                    "$.controls",
                    expected: "NUMBER, RANGE, or SELECT control for every planned SIMULATION component");
        }

        HashSet<string> formulaOutputs = new(StringComparer.Ordinal);
        for (var formulaIndex = 0; formulaIndex < response.Formulas.Count; formulaIndex++)
        {
            var formula = response.Formulas[formulaIndex];
            if (!componentById.TryGetValue(formula.ComponentId, out var component) ||
                component.Kind is not VisualBriefingComponentKind.SIMULATION)
                return Invalid(
                    "A formula must target a planned simulation.",
                    VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                    $"$.formulas[{formulaIndex}].componentId",
                    "componentId",
                    "planned SIMULATION component ID");
            
            if (!component.Slots.Any(slot =>
                    slot.Role is VisualBriefingSlotRole.RESULT &&
                    string.Equals(slot.SlotId, formula.OutputSlotId, StringComparison.Ordinal)))
                return Invalid(
                    "A formula output must target a slot of its simulation.",
                    VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                    $"$.formulas[{formulaIndex}].outputSlotId",
                    "outputSlotId",
                    "slot ID planned for the same SIMULATION component");
            
            if (!formulaOutputs.Add(formula.OutputSlotId))
                return Invalid(
                    "Formula output slots must be unique.",
                    VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                    $"$.formulas[{formulaIndex}].outputSlotId",
                    "outputSlotId",
                    "unique simulation output slot ID");
            
            var simulationControlIds = response.Controls
                .Where(control => control.ComponentId == formula.ComponentId)
                .Select(control => control.ControlId)
                .ToHashSet(StringComparer.Ordinal);
            
            var formulaIssue = ValidateFormulaNode(
                formula.Formula,
                $"$.formulas[{formulaIndex}].formula",
                0,
                simulationControlIds);
            
            if (formulaIssue is not null)
                return formulaIssue;
        }
        
        var simulationWithoutFormula = components.FirstOrDefault(component =>
            component.Kind is VisualBriefingComponentKind.SIMULATION &&
            response.Formulas.All(formula => formula.ComponentId != component.ComponentId));
        
        if (simulationWithoutFormula is not null)
            return Invalid(
                "Every simulation requires at least one formula.",
                VisualBriefingValidationRule.FORMULA_TARGET_INVALID,
                "$.formulas",
                expected: "at least one formula for every planned SIMULATION component");

        var accessibilityIssue = ValidateComponentTexts(
            response.AccessibilityTexts,
            VisualBriefingComponentTexts.AccessibilityTextKeys(components),
            "accessibilityTexts");
        
        if (accessibilityIssue is not null)
            return accessibilityIssue;
        
        return ContainsForbidden(response)
            ? Invalid(
                "Content must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    internal static VisualBriefingContractIssue? ValidateDesign(VisualBriefingPlanArtifact plan, VisualBriefingDesignResponse response)
    {
        if (response.ContractVersion != VisualBriefingVersions.DESIGN_CONTRACT)
            return Invalid(
                "The design response uses an unsupported contract version.",
                VisualBriefingValidationRule.CONTRACT_VERSION_UNSUPPORTED);
        
        if (response.Layout.Kind is not VisualBriefingLayoutNodeKind.STACK ||
            response.Layout.SectionId is not null ||
            response.Layout.ComponentId is not null)
            return Invalid(
                "The design layout requires one STACK root.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        
        var orderedSections = response.Layout.Children.OrderBy(child => child.Order).ToArray();
        if (orderedSections.Length != plan.Sections.Count ||
            orderedSections.Where((node, index) =>
                node.Kind is not VisualBriefingLayoutNodeKind.SECTION ||
                !string.Equals(node.SectionId, plan.Sections[index].SectionId, StringComparison.Ordinal)).Any())
            return Invalid(
                "The layout must contain every planned section exactly once and in plan order.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        
        List<string> references = [];
        List<string> nodeIds = [];
        
        var issue = ValidateLayoutNode(response.Layout, references, nodeIds, true);
        if (issue is not null)
            return issue;
        
        var reserved = plan.Sections.Select(section => section.SectionId)
            .Concat(plan.Sections.SelectMany(section => section.Components).Select(component => component.ComponentId))
            .ToHashSet(StringComparer.Ordinal);
        
        if (nodeIds.Distinct(StringComparer.Ordinal).Count() != nodeIds.Count || nodeIds.Any(reserved.Contains))
            return Invalid(
                "Layout node IDs must be unique and must not collide with section or component IDs.",
                VisualBriefingValidationRule.ID_INVALID);
        
        foreach (var section in plan.Sections)
        {
            var layoutSection = orderedSections.First(node => string.Equals(node.SectionId, section.SectionId, StringComparison.Ordinal));
            List<string> sectionReferences = [];
            CollectComponentReferences(layoutSection, sectionReferences);
            
            var plannedComponents = section.Components.Select(component => component.ComponentId).ToHashSet(StringComparer.Ordinal);
            if (sectionReferences.Count != plannedComponents.Count ||
                sectionReferences.Distinct(StringComparer.Ordinal).Count() != sectionReferences.Count ||
                !sectionReferences.ToHashSet(StringComparer.Ordinal).SetEquals(plannedComponents))
                return Invalid(
                    "Every layout section must reference exactly its own planned components.",
                    VisualBriefingValidationRule.LAYOUT_INVALID);
        }

        // The caller compiles the validated layout right afterwards and guards that compilation as a
        // compiler invariant, see VisualBriefingCompilerInvariant. There is no trial compilation here.
        return ContainsForbidden(response)
            ? Invalid(
                "Design must not contain HTML, CSS, JavaScript, runtime bindings, or chart-library options.",
                VisualBriefingValidationRule.MODEL_MARKUP_PROHIBITED)
            : null;
    }

    private static VisualBriefingContractIssue? ValidateLayoutNode(VisualBriefingLayoutNode node, List<string> references, List<string> nodeIds, bool isRoot = false)
    {
        if (!IsUsableId(node.NodeId) || node.Span is < 1 or > 12 || node.Order is < 0 or > 1000)
            return Invalid(
                "A layout node contains an invalid ID, span, or order.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        
        nodeIds.Add(node.NodeId);
        if (node.Kind is VisualBriefingLayoutNodeKind.COMPONENT)
        {
            if (node.SectionId is not null ||
                string.IsNullOrWhiteSpace(node.ComponentId) ||
                node.Children.Count != 0 ||
                node.Columns is not null)
                return Invalid(
                    "Component layout nodes may only contain a component reference.",
                    VisualBriefingValidationRule.LAYOUT_INVALID);
            
            references.Add(node.ComponentId);
            return null;
        }
        
        if (node.ComponentId is not null ||
            node.Children.Count == 0 ||
            node.Kind is VisualBriefingLayoutNodeKind.SECTION && string.IsNullOrWhiteSpace(node.SectionId) ||
            node.Kind is not VisualBriefingLayoutNodeKind.SECTION && node.SectionId is not null ||
            !isRoot && node.Children.Any(child => child.Kind is VisualBriefingLayoutNodeKind.SECTION))
            return Invalid(
                "Container layout nodes require children and cannot reference a component.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        
        if (node.Kind is VisualBriefingLayoutNodeKind.GRID &&
            (node.Columns is null ||
             node.Columns.Mobile is < 1 or > 4 ||
             node.Columns.Tablet is < 1 or > 8 ||
             node.Columns.Desktop is < 1 or > 12))
            return Invalid(
                "Grid nodes require valid responsive column counts.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        
        if (node.Kind is not VisualBriefingLayoutNodeKind.GRID && node.Columns is not null)
            return Invalid(
                "Responsive columns are only valid for grid nodes.",
                VisualBriefingValidationRule.LAYOUT_INVALID);
        
        foreach (var child in node.Children)
        {
            var issue = ValidateLayoutNode(child, references, nodeIds);
            if (issue is not null)
                return issue;
        }
        
        return null;
    }

    private static void CollectComponentReferences(VisualBriefingLayoutNode node, List<string> references)
    {
        if (node.Kind is VisualBriefingLayoutNodeKind.COMPONENT && node.ComponentId is not null)
            references.Add(node.ComponentId);
        
        foreach (var child in node.Children)
            CollectComponentReferences(child, references);
    }

    private static bool HasValidSlotPattern(VisualBriefingPlanComponent component)
    {
        var roles = component.Slots.Select(slot => slot.Role).ToArray();
        if (component.Slots.Count == 0 ||
            !UniqueIds(component.Slots.Select(slot => slot.SlotId)))
            return false;
        
        return component.Kind switch
        {
            VisualBriefingComponentKind.TEXT => roles.SequenceEqual([VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.BODY]),
            VisualBriefingComponentKind.METRIC => roles.SequenceEqual([VisualBriefingSlotRole.LABEL, VisualBriefingSlotRole.VALUE, VisualBriefingSlotRole.CONTEXT]),
            VisualBriefingComponentKind.CALLOUT => roles.SequenceEqual([VisualBriefingSlotRole.EYEBROW, VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.BODY]),
            VisualBriefingComponentKind.CHART or VisualBriefingComponentKind.ASSET => roles.SequenceEqual([VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.CAPTION]),
            VisualBriefingComponentKind.TABLE or VisualBriefingComponentKind.FILTERABLE_TABLE => roles.SequenceEqual([VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.SUMMARY, VisualBriefingSlotRole.TABLE_DATA]),
            VisualBriefingComponentKind.TABS => roles is [VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.SUMMARY, _, ..] && roles.Skip(2).All(role => role is VisualBriefingSlotRole.PANEL),
            VisualBriefingComponentKind.ACCORDION => roles.SequenceEqual([VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.BODY]),
            VisualBriefingComponentKind.SIMULATION => roles is [VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.SUMMARY, _, ..] && roles.Skip(2).All(role => role is VisualBriefingSlotRole.RESULT),
            VisualBriefingComponentKind.TIMELINE => roles.SequenceEqual([VisualBriefingSlotRole.TITLE, VisualBriefingSlotRole.SUMMARY, VisualBriefingSlotRole.TIMELINE_DATA]),
            
            _ => false,
        };
    }

    private static bool ContainsForbidden<T>(T value)
    {
        var json = JsonSerializer.SerializeToElement(value, VisualBriefingJson.Canonical);
        return ContainsForbiddenElement(json);
    }

    private static bool ContainsForbiddenElement(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Array)
            return value.EnumerateArray().Any(ContainsForbiddenElement);
        
        if (value.ValueKind is JsonValueKind.Object)
            return value.EnumerateObject().Any(property =>
                property.Name is "html" or "templateHtml" or "css" or "script" or "echarts" ||
                ContainsForbiddenElement(property.Value));
        
        if (value.ValueKind is not JsonValueKind.String)
            return false;
        
        var text = value.GetString() ?? string.Empty;
        return FORBIDDEN_MODEL_TEXT.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
               ScriptAccessRegex().IsMatch(text) ||
               HtmlMarkupRegex().IsMatch(text) ||
               CssSnippetRegex().IsMatch(text);
    }

    private static bool UniqueIds(IEnumerable<string> values)
    {
        var items = values.ToArray();
        return items.Length > 0 &&
               items.All(value => ID.IsMatch(value)) &&
               items.Distinct(StringComparer.Ordinal).Count() == items.Length;
    }

    private static VisualBriefingContractIssue? ValidateFormulaNode(VisualBriefingFormulaNode node, string path, int depth, IReadOnlySet<string> controlIds)
    {
        if (depth > 32)
            return Invalid(
                "A formula exceeds the maximum supported depth.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                path,
                expected: "formula depth at most 32");
        
        if (depth == 0 && node.FormulaVersion != VisualBriefingVersions.FORMULA)
            return Invalid(
                "The formula root uses an unsupported version.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.formulaVersion",
                "formulaVersion",
                "supported formula version");
        
        if (depth > 0 &&
            node.FormulaVersion is not 0 &&
            node.FormulaVersion != VisualBriefingVersions.FORMULA)
            return Invalid(
                "A nested formula node uses an unsupported version.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.formulaVersion",
                "formulaVersion",
                "zero or supported formula version");
        
        var hasPath = !string.IsNullOrWhiteSpace(node.Path);
        var hasValue = node.Value is not null;
        var hasOperation = !string.IsNullOrWhiteSpace(node.Operation);
        
        if (new[] { hasPath, hasValue, hasOperation }.Count(value => value) != 1)
            return Invalid(
                "Every formula node must contain exactly one node kind.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                path,
                expected: "exactly one of path, value, or op");
        
        if (hasPath)
        {
            if (node.Arguments is not null)
                return Invalid(
                    "A formula path node must not contain arguments.",
                    VisualBriefingValidationRule.FORMULA_AST_INVALID,
                    $"{path}.args",
                    "args",
                    "omitted");
            
            const string PREFIX = "interactions.state.";
            if (!node.Path!.StartsWith(PREFIX, StringComparison.Ordinal) ||
                !controlIds.Contains(node.Path[PREFIX.Length..]))
                return Invalid(
                    "A formula path must reference a control of the same simulation.",
                    VisualBriefingValidationRule.FORMULA_AST_INVALID,
                    $"{path}.path",
                    "path",
                    "interactions.state.<controlId>");
            
            return null;
        }
        
        if (hasValue)
            return node.Arguments is null
                ? null
                : Invalid(
                    "A formula value node must not contain arguments.",
                    VisualBriefingValidationRule.FORMULA_AST_INVALID,
                    $"{path}.args",
                    "args",
                    "omitted");
        
        HashSet<string> operators = new(StringComparer.Ordinal)
        {
            "add", "subtract", "multiply", "divide", "power", "eq", "ne", "gt", "gte", "lt", "lte",
            "if", "min", "max", "round", "sqrt", "log", "exp",
        };
        
        if (!operators.Contains(node.Operation!))
            return Invalid(
                "A formula uses an unsupported operation.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.op",
                "op",
                "supported formula operation");
        
        if (node.Arguments is null)
            return Invalid(
                "A formula operation requires arguments.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.args",
                "args",
                "argument array with valid arity");
        
        var count = node.Arguments.Count;
        var validArity = node.Operation switch
        {
            "sqrt" or "log" or "exp" => count == 1,
            "subtract" or "divide" or "power" or "eq" or "ne" or "gt" or "gte" or "lt" or "lte" => count == 2,
            "if" => count == 3,
            "round" => count is 1 or 2,
            _ => count > 0,
        };
        
        if (!validArity)
            return Invalid(
                "A formula operation has an invalid number of arguments.",
                VisualBriefingValidationRule.FORMULA_AST_INVALID,
                $"{path}.args",
                "args",
                "argument array with valid arity");
        
        for (var argumentIndex = 0; argumentIndex < node.Arguments.Count; argumentIndex++)
        {
            var issue = ValidateFormulaNode(
                node.Arguments[argumentIndex],
                $"{path}.args[{argumentIndex}]",
                depth + 1,
                controlIds);
            
            if (issue is not null)
                return issue;
        }
        
        return null;
    }

    /// <summary>
    /// Checks whether every row of a validated table slot starts with a text cell.
    /// </summary>
    /// <param name="tableData">The validated table slot value.</param>
    /// <returns>True when every first cell is a string.</returns>
    private static bool HasTextFirstColumn(JsonElement tableData) =>
        tableData.ValueKind is JsonValueKind.Object &&
        tableData.TryGetProperty("rows", out var rows) &&
        rows.ValueKind is JsonValueKind.Array &&
        rows.EnumerateArray().All(row =>
            row.TryGetProperty("cells", out var cells) &&
            cells.ValueKind is JsonValueKind.Array &&
            cells.GetArrayLength() > 0 &&
            cells[0].ValueKind is JsonValueKind.String);

    /// <summary>
    /// Checks one component text map against the component IDs that actually consume it. Asking for
    /// texts that are never rendered is as much a defect as missing the ones that are.
    /// </summary>
    /// <param name="texts">The model-supplied map.</param>
    /// <param name="requiredKeys">The component IDs that consume this kind of text.</param>
    /// <param name="field">The contract field name used in diagnostics.</param>
    /// <returns>The contract issue, or null when the map is complete and exact.</returns>
    private static VisualBriefingContractIssue? ValidateComponentTexts(IReadOnlyDictionary<string, string> texts, IReadOnlyList<string> requiredKeys, string field)
    {
        var required = requiredKeys.ToHashSet(StringComparer.Ordinal);
        var unknownKey = texts.Keys.FirstOrDefault(key => !required.Contains(key));
        if (unknownKey is not null)
            return Invalid(
                $"The {field} contain an entry for a component that does not use one.",
                VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID,
                $"$.{field}.*",
                field,
                "only component IDs that require this text");
        
        foreach (var key in requiredKeys)
        {
            if (!texts.TryGetValue(key, out var text))
                return Invalid(
                    $"A required entry is missing from {field}.",
                    VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID,
                    $"$.{field}",
                    field,
                    "one entry for every component ID that requires this text");
            
            if (string.IsNullOrWhiteSpace(text))
                return Invalid(
                    $"An entry in {field} must not be empty.",
                    VisualBriefingValidationRule.ACCESSIBILITY_TEXT_INVALID,
                    $"$.{field}.{key}",
                    field,
                    "non-empty target-language string");
        }
        
        return texts.Count == required.Count
            ? null
            : Invalid(
                $"The {field} must contain exactly one entry per requiring component.",
                VisualBriefingValidationRule.ACCESSIBILITY_SET_INVALID,
                $"$.{field}",
                field,
                "exactly one entry for every component ID that requires this text");
    }

    private static VisualBriefingContractIssue? ValidateControlState(VisualBriefingControlSpec control, int controlIndex)
    {
        var optionValues = control.Options.Select(option => option.Value).ToArray();
        HashSet<string> seenOptions = new(StringComparer.Ordinal);
        for (var optionIndex = 0; optionIndex < control.Options.Count; optionIndex++)
        {
            var option = control.Options[optionIndex];

            // Option values are pure data: they are compared against the control state and never
            // become element IDs, so they may carry the same text as the data they select:
            if (string.IsNullOrWhiteSpace(option.Value) ||
                option.Value.Length > MAX_OPTION_VALUE_LENGTH ||
                !seenOptions.Add(option.Value))
                return Invalid(
                    "Control option values must be non-empty, short, and unique.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].options[{optionIndex}].value",
                    "value",
                    "unique non-empty string");
            
            if (string.IsNullOrWhiteSpace(option.Label))
                return Invalid(
                    "Control option labels must not be empty.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].options[{optionIndex}].label",
                    "label",
                    "non-empty target-language string");
        }
        
        if (control.Kind is VisualBriefingControlKind.TAB or VisualBriefingControlKind.FILTER or VisualBriefingControlKind.SELECT)
        {
            if (optionValues.Length == 0)
                return Invalid(
                    "This control kind requires options.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].options",
                    "options",
                    "non-empty option array");
            
            if (control.InitialValue.ValueKind is not JsonValueKind.String ||
                !optionValues.Contains(control.InitialValue.GetString(), StringComparer.Ordinal))
                return Invalid(
                    "The initial control value must select one declared option.",
                    VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                    $"$.controls[{controlIndex}].initialValue",
                    "initialValue",
                    "string equal to one option value");
            
            return null;
        }
        
        if (optionValues.Length != 0)
            return Invalid(
                "Numeric controls must not declare options.",
                VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                $"$.controls[{controlIndex}].options",
                "options",
                "empty array");
        
        return control.InitialValue.ValueKind is JsonValueKind.Number
            ? null
            : Invalid(
                "Numeric controls require a numeric initial value.",
                VisualBriefingValidationRule.CONTROL_STATE_INVALID,
                $"$.controls[{controlIndex}].initialValue",
                "initialValue",
                "JSON number");
    }

    private static bool ControlMatchesComponent(VisualBriefingControlKind control, VisualBriefingComponentKind component) => component switch
        {
            VisualBriefingComponentKind.TABS => control is VisualBriefingControlKind.TAB,
            VisualBriefingComponentKind.SIMULATION => control is VisualBriefingControlKind.NUMBER or VisualBriefingControlKind.RANGE or VisualBriefingControlKind.SELECT,

            // FILTER controls are generated from the table data, never supplied by the model:
            _ => false,
        };

    private static string ExpectedControlKinds(VisualBriefingComponentKind component) => component switch
    {
        VisualBriefingComponentKind.TABS => "TAB",
        VisualBriefingComponentKind.SIMULATION => "NUMBER, RANGE, or SELECT",
        
        _ => "no controls",
    };

    private static string? FindInvalidOrDuplicateId(IEnumerable<(string Id, string Path)> candidates)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!IsUsableId(candidate.Id) || !seen.Add(candidate.Id))
                return candidate.Path;
        }
        
        return null;
    }

    /// <summary>
    /// Checks whether an ID is well-formed and free of the reserved AI Studio prefix. Compiled
    /// element IDs are derived from these IDs, and the artifact contract reserves the mwai- prefix.
    /// </summary>
    /// <param name="id">The model-supplied ID.</param>
    /// <returns>True when the ID can be used.</returns>
    private static bool IsUsableId(string id) => ID.IsMatch(id) && !id.StartsWith("mwai-", StringComparison.OrdinalIgnoreCase);

    private static int FindDuplicateIndex(IReadOnlyList<string> values)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            if (!seen.Add(values[index]))
                return index;
        }
        
        return -1;
    }

    private static VisualBriefingContractIssue Invalid(string issue, VisualBriefingValidationRule rule = VisualBriefingValidationRule.NONE, string jsonPath = "$", string fieldName = "", string expected = "") => new(
            VisualBriefingFailureCode.RESPONSE_CONTRACT_INVALID,
            issue,
            rule,
            new()
            {
                IssueKind = VisualBriefingStructuredResponseIssueKind.SEMANTIC_CONTRACT_INVALID,
                JsonPath = jsonPath,
                FieldName = fieldName,

                // Expected carries a contract shape, never a rule name. The rule is reported
                // separately, so an unknown shape stays empty:
                Expected = expected,
            });

    [GeneratedRegex("^[a-z][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    // Matches scripted member access such as document.getElementById( but not a sentence that
    // happens to end with the word "document":
    [GeneratedRegex(@"\b(?:document|window|globalThis)\.[A-Za-z_$][A-Za-z0-9_$]*\s*[({=\[.]", RegexOptions.CultureInvariant)]
    private static partial Regex ScriptAccessRegex();

    // Matches real HTML tags only. A generic "<...>" pattern would reject ordinary prose such as
    // comparisons or placeholders in angle brackets:
    [GeneratedRegex(
        @"<\s*/?\s*(?:script|style|iframe|object|embed|link|meta|form|input|button|select|option|template|svg|img|video|audio|canvas|table|thead|tbody|tfoot|tr|td|th|caption|div|span|p|a|ul|ol|li|dl|dt|dd|h[1-6]|section|article|aside|header|footer|main|nav|figure|figcaption|details|summary|small|strong|em|b|i|u|br|hr|label|fieldset|legend|output|progress)\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlMarkupRegex();

    [GeneratedRegex(@"(?:^|\s)[.#]?[A-Za-z][A-Za-z0-9 _-]*\s*\{[^{}]*:[^{}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex CssSnippetRegex();
}