using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Turns a validated chart specification into a chart-library option object.
/// </summary>
internal sealed class VisualBriefingChartCompiler
{
    internal JsonElement Compile(VisualBriefingChartSpec chart)
    {
        object series = chart.Kind switch
        {
            VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT =>
                chart.Categories.Select((category, index) => new
                {
                    name = category,
                    value = chart.Series[0].Values[index],
                }).ToArray(),
            VisualBriefingChartKind.RADAR => chart.Series.Select(item => new
            {
                name = item.Name,
                type = "radar",
                data = new[]
                {
                    new
                    {
                        value = item.Values,
                        name = item.Name,
                    },
                },
            }).ToArray(),
            _ => chart.Series.Select(item => new
            {
                name = item.Name,
                type = SeriesType(chart.Kind),
                stack = chart.Kind is VisualBriefingChartKind.STACKED_BAR ? "total" : null,
                areaStyle = chart.Kind is VisualBriefingChartKind.AREA ? new { } : null,
                data = item.Values,
            }).ToArray(),
        };
        var option = new
        {
            title = new { text = chart.Title },
            tooltip = new { trigger = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT ? "item" : "axis" },
            legend = new { show = true },
            xAxis = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new { type = "category", data = chart.Categories },
            yAxis = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT or VisualBriefingChartKind.RADAR
                ? null
                : new { type = "value" },
            radar = chart.Kind is VisualBriefingChartKind.RADAR
                ? new { indicator = chart.Categories.Select(name => new { name }).ToArray() }
                : null,
            series = chart.Kind is VisualBriefingChartKind.PIE or VisualBriefingChartKind.DONUT
                ? new[] { new { type = "pie", radius = chart.Kind is VisualBriefingChartKind.DONUT ? new[] { "45%", "70%" } : new[] { "0%", "70%" }, data = series } }
                : series,
        };
        return JsonSerializer.SerializeToElement(option, VisualBriefingJson.Compact);
    }

    private static string SeriesType(VisualBriefingChartKind kind) => kind switch
    {
        VisualBriefingChartKind.LINE or VisualBriefingChartKind.AREA => "line",
        VisualBriefingChartKind.BAR or VisualBriefingChartKind.STACKED_BAR => "bar",
        VisualBriefingChartKind.SCATTER => "scatter",
        VisualBriefingChartKind.RADAR => "radar",
        _ => "line",
    };
}

/// <summary>
/// Compiles the interaction state and the declarative markup of the briefing controls.
/// </summary>
internal sealed class VisualBriefingInteractionCompiler
{
    internal JsonElement Compile(
        IReadOnlyList<VisualBriefingControlSpec> controls,
        IReadOnlyList<VisualBriefingFormulaSpec> formulas)
    {
        var state = controls.ToDictionary(
            control => control.ControlId,
            control => control.InitialValue.Clone(),
            StringComparer.Ordinal);
        var formulaMap = formulas.ToDictionary(
            formula => formula.OutputSlotId,
            formula => formula.Formula,
            StringComparer.Ordinal);
        return JsonSerializer.SerializeToElement(new
        {
            controls,
            state,
            formulas = formulaMap,
        }, VisualBriefingJson.Compact);
    }

    internal string CompileMarkup(string componentId, IReadOnlyList<VisualBriefingControlSpec> controls)
    {
        var builder = new StringBuilder();
        foreach (var indexed in controls.Select((control, index) => (Control: control, Index: index))
                     .Where(item => item.Control.ComponentId == componentId))
        {
            var control = indexed.Control;

            // Controls carry no element ID: nothing references it, and a model-chosen control ID
            // could otherwise collide with a layout node ID in the compiled template:
            var id = HtmlEncoder.Default.Encode(control.ControlId);
            var accessibilityPath = $"accessibility.{HtmlEncoder.Default.Encode(componentId)}";
            builder.Append(control.Kind switch
            {
                VisualBriefingControlKind.SELECT or VisualBriefingControlKind.FILTER =>
                    $"<select data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\"><template data-mwai-each=\"interactions.controls.{indexed.Index}.options\"><option data-mwai-attr-value=\".value\" data-mwai-text=\".label\"></option></template></select>",
                VisualBriefingControlKind.RANGE =>
                    $"<input type=\"range\" data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\">",
                VisualBriefingControlKind.NUMBER =>
                    $"<input type=\"number\" data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\">",
                _ => string.Empty,
            });
        }
        return builder.ToString();
    }

    internal static string CompileResetMarkup(string componentId) =>
        $"<button type=\"button\" data-mwai-reset=\"{HtmlEncoder.Default.Encode(componentId)}\" data-mwai-text=\"labels.reset\"></button>";
}

/// <summary>
/// Compiles the validated plan, content, and layout into the declarative template and stylesheet.
/// </summary>
internal sealed class VisualBriefingLayoutCompiler(
    VisualBriefingChartCompiler chartCompiler,
    VisualBriefingInteractionCompiler interactionCompiler)
{
    internal VisualBriefingCompilationResult Compile(
        VisualBriefingPlanArtifact plan,
        VisualBriefingContentArtifact content,
        VisualBriefingLayoutNode layout,
        VisualBriefingDesignTokens tokens)
    {
        var slots = content.Slots.ToDictionary(item => item.SlotId, item => item.Value.Clone(), StringComparer.Ordinal);
        var components = plan.Sections.SelectMany(section => section.Components)
            .ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        var charts = content.Charts.ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        var missingSlot = components.Values
            .SelectMany(component => component.RequiredSlots)
            .FirstOrDefault(slotId => !slots.ContainsKey(slotId));
        if (missingSlot is not null)
            throw new InvalidDataException("A planned content slot is missing during compilation.");
        var missingChart = components.Values
            .Where(component => component.Kind is VisualBriefingComponentKind.CHART)
            .Select(component => component.ComponentId)
            .FirstOrDefault(componentId => !charts.ContainsKey(componentId));
        if (missingChart is not null)
            throw new InvalidDataException("A planned chart is missing during compilation.");
        var chartOptions = content.Charts.ToDictionary(
            item => item.ComponentId,
            item => chartCompiler.Compile(item),
            StringComparer.Ordinal);
        var interactions = interactionCompiler.Compile(content.Controls, content.Formulas);
        var data = JsonSerializer.SerializeToElement(new
        {
            slots,
            charts = chartOptions,
            interactions,
            accessibility = content.AccessibilityTexts,
            visibleLabels = content.VisibleLabels,
            sourceReferences = content.SourceReferences,
            labels = new { reset = content.ResetLabel },
        }, VisualBriefingJson.Compact);
        var html = this.CompileNode(layout, components, content);
        var css = CompileCss(tokens, layout);
        return new(
            data,
            html,
            css,
            VisualBriefingHashing.Compute(html),
            VisualBriefingHashing.Compute(css));
    }

    private string CompileNode(
        VisualBriefingLayoutNode node,
        IReadOnlyDictionary<string, VisualBriefingPlanComponent> components,
        VisualBriefingContentArtifact content)
    {
        var id = HtmlEncoder.Default.Encode(node.NodeId);
        if (node.Kind is VisualBriefingLayoutNodeKind.COMPONENT)
        {
            if (node.ComponentId is null || !components.TryGetValue(node.ComponentId, out var component))
                throw new InvalidDataException("The layout references an unknown component.");
            var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
            var body = this.CompileComponent(component, content);
            var componentClasses = CompileLayoutClasses(
                node,
                $"mwai-component mwai-{component.Kind.ToString().ToLowerInvariant()}");
            return $"<article id=\"{id}\" class=\"{componentClasses}\" data-mwai-region=\"{componentId}\">{body}</article>";
        }
        var tag = node.Kind is VisualBriefingLayoutNodeKind.SECTION ? "section" : "div";
        var kind = node.Kind.ToString().ToLowerInvariant();
        var layoutClasses = CompileLayoutClasses(node, $"mwai-layout mwai-{kind}");
        var children = string.Concat(node.Children.OrderBy(child => child.Order)
            .Select(child => this.CompileNode(child, components, content)));
        return $"<{tag} id=\"{id}\" class=\"{layoutClasses}\">{children}</{tag}>";
    }

    private static string CompileLayoutClasses(VisualBriefingLayoutNode node, string prefix) =>
        $"{prefix} mwai-span-{node.Span} mwai-align-{node.Alignment.ToString().ToLowerInvariant()}" +
        (node.Emphasized ? " mwai-emphasized" : string.Empty);

    private string CompileComponent(
        VisualBriefingPlanComponent component,
        VisualBriefingContentArtifact content)
    {
        var componentId = HtmlEncoder.Default.Encode(component.ComponentId);

        // Block elements, not spans: consecutive slots would otherwise render as one run of text:
        var slotMarkup = string.Concat(component.RequiredSlots.Select(slotId =>
        {
            var encoded = HtmlEncoder.Default.Encode(slotId);
            return $"<p data-mwai-text=\"slots.{encoded}\"></p>";
        }));
        var controls = interactionCompiler.CompileMarkup(component.ComponentId, content.Controls);
        var formulas = string.Concat(content.Formulas
            .Where(formula => formula.ComponentId == component.ComponentId)
            .Select(formula =>
                $"<span data-mwai-expr=\"interactions.formulas.{HtmlEncoder.Default.Encode(formula.OutputSlotId)}\"></span>"));
        var filterControl = content.Controls.FirstOrDefault(control =>
            control.ComponentId == component.ComponentId &&
            control.Kind is VisualBriefingControlKind.FILTER);

        // Rows are filtered by their first cell, so the filter options of a filterable table
        // correspond to the values of the table's first column:
        var filterAttributes = filterControl is null
            ? string.Empty
            : $" data-mwai-filter=\"$root.interactions.state.{HtmlEncoder.Default.Encode(filterControl.ControlId)}\" data-mwai-filter-value=\".cells.0\"";
        var body = component.Kind switch
        {
            VisualBriefingComponentKind.CHART =>
                $"<figure><div role=\"img\" data-mwai-attr-aria-label=\"accessibility.{HtmlEncoder.Default.Encode(component.ComponentId)}\" aria-describedby=\"{componentId}-chart-alt\" data-mwai-chart=\"charts.{HtmlEncoder.Default.Encode(component.ComponentId)}\"></div><figcaption id=\"{componentId}-chart-alt\">{slotMarkup}</figcaption></figure>",
            VisualBriefingComponentKind.ASSET =>
                $"<figure><img data-mwai-asset=\"{HtmlEncoder.Default.Encode(component.AssetId ?? throw new InvalidDataException("An asset component is missing its asset ID."))}\" data-mwai-attr-alt=\"accessibility.{componentId}\"><figcaption>{slotMarkup}</figcaption></figure>",
            VisualBriefingComponentKind.TABLE or VisualBriefingComponentKind.FILTERABLE_TABLE =>
                CompileTable(component, componentId, controls, filterAttributes),
            VisualBriefingComponentKind.TABS =>
                this.CompileTabs(component, content.Controls),
            VisualBriefingComponentKind.ACCORDION =>
                $"<details><summary><span data-mwai-text=\"visibleLabels.{componentId}\"></span></summary><div>{slotMarkup}</div></details>",
            VisualBriefingComponentKind.SIMULATION =>
                $"<div class=\"mwai-simulation\">{controls}{slotMarkup}{formulas}{VisualBriefingInteractionCompiler.CompileResetMarkup(component.ComponentId)}</div>",
            _ => $"{slotMarkup}{controls}",
        };
        var references = content.SourceReferences.ContainsKey(component.ComponentId)
            ? $"<small><template data-mwai-each=\"sourceReferences.{componentId}\"><span data-mwai-text=\".\"></span> </template></small>"
            : string.Empty;
        return $"{body}{references}";
    }

    /// <summary>
    /// Compiles a table component from its tabular data slot. The first required slot carries the
    /// columns and rows, see VisualBriefingSlotTypes; any further slot is rendered as leading text.
    /// </summary>
    /// <param name="component">The planned table component.</param>
    /// <param name="componentId">The encoded component ID.</param>
    /// <param name="controls">The compiled control markup of the component.</param>
    /// <param name="filterAttributes">The compiled row filter attributes, if any.</param>
    /// <returns>The compiled table markup.</returns>
    private static string CompileTable(
        VisualBriefingPlanComponent component,
        string componentId,
        string controls,
        string filterAttributes)
    {
        var dataSlot = HtmlEncoder.Default.Encode(component.RequiredSlots[0]);
        var leadingText = string.Concat(component.RequiredSlots.Skip(1).Select(slotId =>
            $"<p data-mwai-text=\"slots.{HtmlEncoder.Default.Encode(slotId)}\"></p>"));
        return $"{leadingText}<div class=\"mwai-table-wrap\">{controls}<table>" +
               $"<caption data-mwai-text=\"visibleLabels.{componentId}\"></caption>" +
               $"<thead><tr><template data-mwai-each=\"slots.{dataSlot}.columns\"><th scope=\"col\" data-mwai-text=\".\"></th></template></tr></thead>" +
               $"<tbody><template data-mwai-each=\"slots.{dataSlot}.rows\"><tr{filterAttributes}><template data-mwai-each=\".cells\"><td data-mwai-text=\".\"></td></template></tr></template></tbody>" +
               "</table></div>";
    }

    private string CompileTabs(
        VisualBriefingPlanComponent component,
        IReadOnlyList<VisualBriefingControlSpec> controls)
    {
        var indexedControl = controls.Select((control, index) => (Control: control, Index: index))
            .First(item =>
                item.Control.ComponentId == component.ComponentId &&
                item.Control.Kind is VisualBriefingControlKind.TAB);
        var initial = indexedControl.Control.InitialValue.GetString();
        var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
        var buttons = new StringBuilder();
        var panels = new StringBuilder();
        for (var index = 0; index < indexedControl.Control.Options.Count; index++)
        {
            var option = indexedControl.Control.Options[index];

            // The panel ID must remain a safe identifier, so it is derived from the option position
            // instead of the model-supplied option value:
            var panelId = $"{componentId}-tab-{index}";
            var selected = string.Equals(option.Value, initial, StringComparison.Ordinal);
            buttons.Append(
                $"<button type=\"button\" role=\"tab\" aria-controls=\"{panelId}\" aria-selected=\"{selected.ToString().ToLowerInvariant()}\" data-mwai-tab-target=\"{panelId}\" data-mwai-text=\"interactions.controls.{indexedControl.Index}.options.{index}.label\"></button>");
            var slotId = component.RequiredSlots[Math.Min(index, component.RequiredSlots.Count - 1)];
            panels.Append(
                $"<section id=\"{panelId}\" role=\"tabpanel\" data-mwai-tab-panel=\"{panelId}\"{(selected ? string.Empty : " hidden")}><p data-mwai-text=\"slots.{HtmlEncoder.Default.Encode(slotId)}\"></p></section>");
        }
        return $"<div data-mwai-tabs=\"{componentId}\"><div role=\"tablist\">{buttons}</div>{panels}</div>";
    }

    private static string CompileCss(
        VisualBriefingDesignTokens tokens,
        VisualBriefingLayoutNode layout)
    {
        var density = tokens.Density switch
        {
            VisualBriefingDensity.COMPACT => 0.75m,
            VisualBriefingDensity.SPACIOUS => 1.25m,
            _ => 1m,
        };
        var shadow = tokens.Surface is VisualBriefingSurface.RAISED
            ? "0 12px 32px rgba(23,32,51,.12)"
            : "none";
        var surface = tokens.Surface switch
        {
            VisualBriefingSurface.SUBTLE => "background:color-mix(in srgb,var(--mwai-bg),var(--mwai-primary) 4%);",
            VisualBriefingSurface.ACCENT => "border:1px solid var(--mwai-accent);",
            _ => string.Empty,
        };
        var typeScale = tokens.TypographyScale switch
        {
            VisualBriefingTypographyScale.COMPACT => 0.9m,
            VisualBriefingTypographyScale.EDITORIAL => 1.1m,
            VisualBriefingTypographyScale.DISPLAY => 1.2m,
            _ => 1m,
        };
        var css = new StringBuilder($$"""
                                    .mwai-layout{--mwai-primary:{{tokens.PrimaryColor}};--mwai-accent:{{tokens.AccentColor}};--mwai-text:{{tokens.TextColor}};--mwai-bg:{{tokens.BackgroundColor}};--mwai-space:{{tokens.SpacingScale}}px;--mwai-radius:{{tokens.Radius}}px;--mwai-density:{{density.ToString(System.Globalization.CultureInfo.InvariantCulture)}};--mwai-type-scale:{{typeScale.ToString(System.Globalization.CultureInfo.InvariantCulture)}};box-sizing:border-box;color:var(--mwai-text);background:var(--mwai-bg);font-size:calc(1rem*var(--mwai-type-scale));gap:calc(var(--mwai-space)*var(--mwai-density)*4);}
                                    .mwai-section,.mwai-stack{display:flex;flex-direction:column;}
                                    .mwai-grid{display:grid;}
                                    .mwai-component{display:flex;flex-direction:column;min-width:0;gap:calc(var(--mwai-space)*var(--mwai-density)*2);padding:calc(var(--mwai-space)*var(--mwai-density)*4);border-radius:var(--mwai-radius);box-shadow:{{shadow}};{{surface}}}
                                    .mwai-emphasized{border-inline-start:4px solid var(--mwai-accent);}
                                    .mwai-align-start{align-items:start;}.mwai-align-center{align-items:center;}.mwai-align-end{align-items:end;}.mwai-align-stretch{align-items:stretch;}
                                    .mwai-table-wrap{overflow:auto;}table{border-collapse:collapse;width:100%;}img{display:block;max-width:100%;height:auto;}
                                    [data-mwai-chart]{width:100%;min-height:20rem;}
                                    """);
        foreach (var grid in EnumerateGridNodes(layout))
        {
            var id = grid.NodeId;
            css.Append($"#{id}{{grid-template-columns:repeat({grid.Columns!.Mobile},minmax(0,1fr));}}");
            foreach (var child in grid.Children)
                css.Append($"#{child.NodeId}{{grid-column:span {Math.Min(child.Span, grid.Columns.Mobile)};}}");
            css.Append($"@media(min-width:48rem){{#{id}{{grid-template-columns:repeat({grid.Columns.Tablet},minmax(0,1fr));}}");
            foreach (var child in grid.Children)
                css.Append($"#{child.NodeId}{{grid-column:span {Math.Min(child.Span, grid.Columns.Tablet)};}}");
            css.Append('}');
            css.Append($"@media(min-width:75rem){{#{id}{{grid-template-columns:repeat({grid.Columns.Desktop},minmax(0,1fr));}}");
            foreach (var child in grid.Children)
                css.Append($"#{child.NodeId}{{grid-column:span {Math.Min(child.Span, grid.Columns.Desktop)};}}");
            css.Append('}');
        }
        return css.ToString();
    }

    private static IEnumerable<VisualBriefingLayoutNode> EnumerateGridNodes(VisualBriefingLayoutNode node)
    {
        if (node.Kind is VisualBriefingLayoutNodeKind.GRID)
            yield return node;
        foreach (var child in node.Children)
        foreach (var grid in EnumerateGridNodes(child))
            yield return grid;
    }
}
