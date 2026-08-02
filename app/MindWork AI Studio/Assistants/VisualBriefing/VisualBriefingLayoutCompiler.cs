using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Compiles validated content into the fixed MindWork editorial presentation system.
/// </summary>
internal sealed class VisualBriefingLayoutCompiler
{
    /// <summary>
    /// Compiles semantic plan, content, layout, and profile artifacts into standalone parts.
    /// </summary>
    /// <param name="plan">The validated semantic plan.</param>
    /// <param name="content">The validated content.</param>
    /// <param name="layout">The validated layout tree.</param>
    /// <param name="profile">The bounded MindWork design profile.</param>
    /// <returns>The deterministic compiled parts and hashes.</returns>
    internal static VisualBriefingCompilationResult Compile(VisualBriefingPlanArtifact plan, VisualBriefingContentArtifact content, VisualBriefingLayoutNode layout, VisualBriefingDesignProfile profile)
    {
        var slots = content.Slots.ToDictionary(item => item.SlotId, item => item.Value.Clone(), StringComparer.Ordinal);
        var plannedSlotIds = plan.Sections
            .SelectMany(section => new[] { section.TitleSlotId, section.SummarySlotId }
                .Concat(section.Components.SelectMany(component => component.Slots.Select(slot => slot.SlotId))))
            .ToArray();
        
        var missingSlot = plannedSlotIds.FirstOrDefault(slotId => !slots.ContainsKey(slotId));
        if (missingSlot is not null)
            throw new InvalidDataException("A planned content slot is missing during compilation.");

        var components = plan.Sections.SelectMany(section => section.Components)
            .ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        
        var sections = plan.Sections.ToDictionary(item => item.SectionId, StringComparer.Ordinal);
        var charts = content.Charts.ToDictionary(item => item.ComponentId, StringComparer.Ordinal);
        var missingChart = components.Values
            .Where(component => component.Kind is VisualBriefingComponentKind.CHART)
            .Select(component => component.ComponentId)
            .FirstOrDefault(componentId => !charts.ContainsKey(componentId));
        
        if (missingChart is not null)
            throw new InvalidDataException("A planned chart is missing during compilation.");

        var chartOptions = content.Charts.ToDictionary(
            item => item.ComponentId,
            item => VisualBriefingChartCompiler.Compile(item),
            StringComparer.Ordinal);
        
        var interactions = VisualBriefingInteractionCompiler.Compile(content.Controls, content.Formulas);
        
        var data = JsonSerializer.SerializeToElement(new
        {
            slots,
            charts = chartOptions,
            interactions,
            accessibility = content.AccessibilityTexts,
            sourceReferences = content.SourceReferences,
            labels = new
            {
                reset = content.ResetLabel,
                brand = "MindWork AI Studio",
            },
        }, VisualBriefingJson.Canonical);
        
        var html = CompileNode(layout, sections, components, content, true);
        var css = CompileCss(profile, layout);
        return new(
            data,
            html,
            css,
            VisualBriefingHashing.Compute(html),
            VisualBriefingHashing.Compute(css));
    }

    private static string CompileNode(VisualBriefingLayoutNode node, IReadOnlyDictionary<string, VisualBriefingPlanSection> sections, IReadOnlyDictionary<string, VisualBriefingPlanComponent> components, VisualBriefingContentArtifact content, bool isRoot = false)
    {
        var id = HtmlEncoder.Default.Encode(node.NodeId);
        if (node.Kind is VisualBriefingLayoutNodeKind.COMPONENT)
        {
            if (node.ComponentId is null || !components.TryGetValue(node.ComponentId, out var component))
                throw new InvalidDataException("The layout references an unknown component.");
            
            var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
            var body = CompileComponent(component, content);
            var semanticClasses = $"mwai-component mwai-{component.Kind.ToString().ToLowerInvariant()}";
            if (component.Kind is VisualBriefingComponentKind.TIMELINE)
            {
                semanticClasses += component.TimelineOrientation switch
                {
                    VisualBriefingTimelineOrientation.HORIZONTAL => " mwai-timeline-horizontal",
                    VisualBriefingTimelineOrientation.VERTICAL => " mwai-timeline-vertical",
                    _ => throw new InvalidDataException("A timeline component has an invalid orientation."),
                };
            }

            var componentClasses = CompileLayoutClasses(node, semanticClasses);
            
            return $"<article id=\"{id}\" class=\"{componentClasses}\" data-mwai-region=\"{componentId}\">{body}</article>";
        }

        var children = string.Concat(node.Children.OrderBy(child => child.Order)
            .Select(child => CompileNode(child, sections, components, content)));
        
        if (node.Kind is VisualBriefingLayoutNodeKind.SECTION)
        {
            if (node.SectionId is null || !sections.TryGetValue(node.SectionId, out var section))
                throw new InvalidDataException("The layout references an unknown section.");
            
            var title = HtmlEncoder.Default.Encode(section.TitleSlotId);
            var summary = HtmlEncoder.Default.Encode(section.SummarySlotId);
            var headingTag = section.Role is VisualBriefingSectionRole.HERO ? "h1" : "h2";
            var role = section.Role.ToString().ToLowerInvariant().Replace('_', '-');
            var classes = CompileLayoutClasses(node, $"mwai-layout mwai-section mwai-section-{role}");
            
            return $"<section id=\"{id}\" class=\"{classes}\"><div class=\"mwai-section-inner\"><header class=\"mwai-section-heading\"><{headingTag} data-mwai-text=\"slots.{title}\"></{headingTag}><p data-mwai-text=\"slots.{summary}\"></p></header><div class=\"mwai-section-content\">{children}</div></div></section>";
        }

        var kind = node.Kind.ToString().ToLowerInvariant();
        var layoutClasses = CompileLayoutClasses(node, $"mwai-layout mwai-{kind}");
        
        if (isRoot)
            return $"<main id=\"{id}\" class=\"{layoutClasses} mwai-document\">{children}</main>";
        
        return $"<div id=\"{id}\" class=\"{layoutClasses}\">{children}</div>";
    }

    private static string CompileLayoutClasses(VisualBriefingLayoutNode node, string prefix) =>
        $"{prefix} mwai-span-{node.Span} mwai-align-{node.Alignment.ToString().ToLowerInvariant()}" +
        (node.Emphasized ? " mwai-emphasized" : string.Empty);

    private static string CompileComponent(VisualBriefingPlanComponent component, VisualBriefingContentArtifact content)
    {
        var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
        var controls = VisualBriefingInteractionCompiler.CompileMarkup(component.ComponentId, content.Controls);
        var body = component.Kind switch
        {
            VisualBriefingComponentKind.TEXT => $"<header class=\"mwai-component-heading\"><h3 data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.TITLE)}\"></h3></header><p class=\"mwai-copy\" data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.BODY)}\"></p>",
            VisualBriefingComponentKind.METRIC => $"<dl class=\"mwai-metric-body\"><dt data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.LABEL)}\"></dt><dd data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.VALUE)}\"></dd></dl><p class=\"mwai-context\" data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.CONTEXT)}\"></p>",
            VisualBriefingComponentKind.CALLOUT => $"<aside><p class=\"mwai-eyebrow\" data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.EYEBROW)}\"></p><h3 data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.TITLE)}\"></h3><p data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.BODY)}\"></p></aside>",
            VisualBriefingComponentKind.CHART => $"<figure><header class=\"mwai-component-heading\"><h3 data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.TITLE)}\"></h3></header><div role=\"img\" data-mwai-attr-aria-label=\"accessibility.{componentId}\" aria-describedby=\"{componentId}-chart-alt\" data-mwai-chart=\"charts.{componentId}\"></div><figcaption id=\"{componentId}-chart-alt\" data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.CAPTION)}\"></figcaption></figure>",
            VisualBriefingComponentKind.ASSET => $"<figure><header class=\"mwai-component-heading\"><h3 data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.TITLE)}\"></h3></header><img data-mwai-asset=\"{HtmlEncoder.Default.Encode(component.AssetId ?? throw new InvalidDataException("An asset component is missing its asset ID."))}\" data-mwai-attr-alt=\"accessibility.{componentId}\"><figcaption data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.CAPTION)}\"></figcaption></figure>",
            VisualBriefingComponentKind.TABLE or VisualBriefingComponentKind.FILTERABLE_TABLE => CompileTable(component, controls, content),
            VisualBriefingComponentKind.TABS => CompileTabs(component, content.Controls),
            VisualBriefingComponentKind.ACCORDION => $"<details><summary><span data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.TITLE)}\"></span></summary><div class=\"mwai-accordion-body\"><p data-mwai-text=\"slots.{Slot(component, VisualBriefingSlotRole.BODY)}\"></p></div></details>",
            VisualBriefingComponentKind.SIMULATION => CompileSimulation(component, controls, content),
            VisualBriefingComponentKind.TIMELINE => CompileTimeline(component),
            
            _ => string.Empty,
        };
        
        var references = content.SourceReferences.ContainsKey(component.ComponentId)
            ? $"<small class=\"mwai-sources\"><template data-mwai-each=\"sourceReferences.{componentId}\"><span data-mwai-text=\".\"></span> </template></small>"
            : string.Empty;
        
        return $"{body}{references}";
    }

    private static string CompileTable(VisualBriefingPlanComponent component, string controls, VisualBriefingContentArtifact content)
    {
        var title = Slot(component, VisualBriefingSlotRole.TITLE);
        var summary = Slot(component, VisualBriefingSlotRole.SUMMARY);
        var dataSlot = Slot(component, VisualBriefingSlotRole.TABLE_DATA);
        
        var filterControl = content.Controls.FirstOrDefault(control =>
            control.ComponentId == component.ComponentId &&
            control.Kind is VisualBriefingControlKind.FILTER);
        
        var filterAttributes = filterControl is null
            ? string.Empty
            : $" data-mwai-filter=\"$root.interactions.state.{HtmlEncoder.Default.Encode(filterControl.ControlId)}\" data-mwai-filter-value=\".cells.0\"";
        
        var toolbar = string.IsNullOrEmpty(controls) ? string.Empty : $"<div class=\"mwai-toolbar\">{controls}</div>";
        return $"<header class=\"mwai-component-heading\"><h3 data-mwai-text=\"slots.{title}\"></h3><p data-mwai-text=\"slots.{summary}\"></p></header>{toolbar}<div class=\"mwai-table-wrap\"><table>" +
               $"<caption><strong data-mwai-text=\"slots.{title}\"></strong><span data-mwai-text=\"slots.{summary}\"></span></caption>" +
               $"<thead><tr><template data-mwai-each=\"slots.{dataSlot}.columns\"><th scope=\"col\" data-mwai-text=\".\"></th></template></tr></thead>" +
               $"<tbody><template data-mwai-each=\"slots.{dataSlot}.rows\"><tr{filterAttributes}><template data-mwai-each=\".cells\"><td data-mwai-text=\".\"></td></template></tr></template></tbody>" +
               "</table></div>";
    }

    private static string CompileTabs(VisualBriefingPlanComponent component, IReadOnlyList<VisualBriefingControlSpec> controls)
    {
        var indexedControl = controls.Select((control, index) => (Control: control, Index: index))
            .First(item =>
                item.Control.ComponentId == component.ComponentId &&
                item.Control.Kind is VisualBriefingControlKind.TAB);
        
        var initial = indexedControl.Control.InitialValue.GetString();
        var componentId = HtmlEncoder.Default.Encode(component.ComponentId);
        var title = Slot(component, VisualBriefingSlotRole.TITLE);
        var summary = Slot(component, VisualBriefingSlotRole.SUMMARY);
        var panelsSlots = component.Slots.Where(slot => slot.Role is VisualBriefingSlotRole.PANEL).ToArray();
        var buttons = new StringBuilder();
        var panels = new StringBuilder();
        
        for (var index = 0; index < indexedControl.Control.Options.Count; index++)
        {
            var option = indexedControl.Control.Options[index];
            var panelId = $"{componentId}-tab-{index}";
            var selected = string.Equals(option.Value, initial, StringComparison.Ordinal);
            buttons.Append($"<button type=\"button\" role=\"tab\" aria-controls=\"{panelId}\" aria-selected=\"{selected.ToString().ToLowerInvariant()}\" data-mwai-tab-target=\"{panelId}\" data-mwai-text=\"interactions.controls.{indexedControl.Index}.options.{index}.label\"></button>");
            panels.Append($"<section id=\"{panelId}\" role=\"tabpanel\" data-mwai-tab-panel=\"{panelId}\"{(selected ? string.Empty : " hidden")}><p data-mwai-text=\"slots.{HtmlEncoder.Default.Encode(panelsSlots[index].SlotId)}\"></p></section>");
        }
        
        return $"<header class=\"mwai-component-heading\"><h3 data-mwai-text=\"slots.{title}\"></h3><p data-mwai-text=\"slots.{summary}\"></p></header><div data-mwai-tabs=\"{componentId}\"><div role=\"tablist\">{buttons}</div>{panels}</div>";
    }

    private static string CompileSimulation(VisualBriefingPlanComponent component, string controls, VisualBriefingContentArtifact content)
    {
        var title = Slot(component, VisualBriefingSlotRole.TITLE);
        var summary = Slot(component, VisualBriefingSlotRole.SUMMARY);
        var outputs = string.Concat(content.Formulas
            .Where(formula => formula.ComponentId == component.ComponentId)
            .Select(formula => $"<output data-mwai-expr=\"interactions.formulas.{HtmlEncoder.Default.Encode(formula.OutputSlotId)}\"></output>"));
        
        return $"<fieldset><legend data-mwai-text=\"slots.{title}\"></legend><p data-mwai-text=\"slots.{summary}\"></p><div class=\"mwai-control-grid\">{controls}</div><div class=\"mwai-results\">{outputs}</div>{VisualBriefingInteractionCompiler.CompileResetMarkup(component.ComponentId)}</fieldset>";
    }

    private static string CompileTimeline(VisualBriefingPlanComponent component)
    {
        var title = Slot(component, VisualBriefingSlotRole.TITLE);
        var summary = Slot(component, VisualBriefingSlotRole.SUMMARY);
        var dataSlot = Slot(component, VisualBriefingSlotRole.TIMELINE_DATA);

        return $"<header class=\"mwai-component-heading\"><h3 data-mwai-text=\"slots.{title}\"></h3><p data-mwai-text=\"slots.{summary}\"></p></header>" +
               $"<ol class=\"mwai-timeline-track\" role=\"list\"><template data-mwai-each=\"slots.{dataSlot}.items\"><li class=\"mwai-timeline-item\">" +
               "<span class=\"mwai-timeline-marker\" aria-hidden=\"true\"></span><div class=\"mwai-timeline-content\">" +
               "<p class=\"mwai-timeline-period\" data-mwai-text=\".period\"></p><h4 data-mwai-text=\".title\"></h4>" +
               "<p class=\"mwai-timeline-description\" data-mwai-text=\".description\"></p></div></li></template></ol>";
    }

    private static string Slot(VisualBriefingPlanComponent component, VisualBriefingSlotRole role, int occurrence = 0)
    {
        var slot = component.Slots.Where(candidate => candidate.Role == role).ElementAtOrDefault(occurrence) ?? throw new InvalidDataException($"A {component.Kind} component is missing its {role} slot.");
        return HtmlEncoder.Default.Encode(slot.SlotId);
    }

    private static string CompileCss(VisualBriefingDesignProfile profile, VisualBriefingLayoutNode layout)
    {
        var (typeScale, rhythm, sectionSpace) = profile switch
        {
            VisualBriefingDesignProfile.EXECUTIVE => ("1.06", "0.92", "4.5rem"),
            VisualBriefingDesignProfile.ANALYTICAL => ("0.96", "0.82", "3.5rem"),
            _ => ("1", "1", "5.5rem"),
        };
        
        var css = new StringBuilder($$"""
                                    #mwai-briefing-root{--mwai-ink:#172A24;--mwai-forest:#164B3B;--mwai-pine:#236A50;--mwai-sage:#79AE90;--mwai-cream:#F7F1DC;--mwai-paper:#FFFEFA;--mwai-sun:#F2D264;--mwai-mist:#EAF1EC;--mwai-clay:#C97857;--mwai-line:#D6E2DC;--mwai-muted:#5E7169;--mwai-type-scale:{{typeScale}};--mwai-rhythm:{{rhythm}};--mwai-section-space:{{sectionSpace}};max-width:80rem;margin-inline:auto;padding:clamp(1rem,2.5vw,2rem) clamp(1rem,3.5vw,3rem) clamp(1rem,3.5vw,3rem);font:calc(1rem*var(--mwai-type-scale))/1.65 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;color:var(--mwai-ink);}
                                    #mwai-briefing-root *{box-sizing:border-box;}
                                    .mwai-document{display:flex;flex-direction:column;gap:clamp(1rem,2.5vw,2rem);}
                                    .mwai-section{display:block;border-radius:clamp(1.25rem,2.5vw,2rem);}
                                    .mwai-section-inner{padding:clamp(2rem,5vw,var(--mwai-section-space));}
                                    .mwai-section-heading{max-width:52rem;margin-block-end:clamp(1.75rem,4vw,3.25rem);}
                                    .mwai-section-heading h1,.mwai-section-heading h2,.mwai-component h3{margin:0;color:inherit;font-weight:720;letter-spacing:-.035em;line-height:1.08;text-wrap:balance;}
                                    .mwai-section-heading h1{font-size:clamp(2.6rem,7vw,5.8rem);max-width:14ch;}
                                    .mwai-section-heading h2{font-size:clamp(2rem,4.2vw,3.55rem);max-width:18ch;}
                                    .mwai-section-heading p{max-width:65ch;margin:1.15rem 0 0;font-size:clamp(1.05rem,1.8vw,1.3rem);line-height:1.55;color:var(--mwai-muted);}
                                    .mwai-section-hero{overflow:hidden;background:linear-gradient(135deg,var(--mwai-forest),#255F4B);color:var(--mwai-paper);}
                                    .mwai-section-hero .mwai-section-inner{min-height:min(43rem,72vh);display:flex;flex-direction:column;position:relative;}
                                    .mwai-section-hero .mwai-section-heading{margin-block-start:auto;}
                                    .mwai-section-hero .mwai-section-heading p{color:color-mix(in srgb,var(--mwai-paper),transparent 18%);}
                                    .mwai-section-hero .mwai-section-heading{margin-block-end:clamp(1.5rem,3vw,2.5rem);}
                                    .mwai-section-executive-summary{background:var(--mwai-cream);}
                                    .mwai-section-evidence{background:var(--mwai-mist);}
                                    .mwai-section-exploration{background:var(--mwai-paper);border:1px solid var(--mwai-line);}
                                    .mwai-section-conclusion{background:var(--mwai-forest);color:var(--mwai-paper);}
                                    .mwai-section-conclusion .mwai-section-heading p{color:color-mix(in srgb,var(--mwai-paper),transparent 18%);}
                                    .mwai-section-narrative{border-radius:0;border-block-start:1px solid var(--mwai-line);}
                                    .mwai-section-content,.mwai-stack{display:flex;flex-direction:column;gap:clamp(1.25rem,3vw,2.25rem);}
                                    .mwai-grid{display:grid;gap:clamp(1rem,2.5vw,2rem);}
                                    .mwai-component{display:flex;flex-direction:column;min-width:0;gap:calc(1rem*var(--mwai-rhythm));}
                                    .mwai-component-heading{display:flex;flex-direction:column;gap:.55rem;}
                                    .mwai-component-heading h3,.mwai-callout h3{font-size:clamp(1.3rem,2.2vw,1.75rem);}
                                    .mwai-component-heading p,.mwai-copy,.mwai-context,.mwai-callout p{margin:0;max-width:70ch;}
                                    .mwai-text{max-width:72ch;padding-block:.5rem;}
                                    .mwai-metric,.mwai-chart,.mwai-asset,.mwai-table,.mwai-filterable_table,.mwai-tabs,.mwai-accordion,.mwai-simulation,.mwai-timeline{padding:clamp(1.25rem,2.5vw,2rem);border:1px solid var(--mwai-line);border-radius:1.25rem;background:color-mix(in srgb,var(--mwai-paper),transparent 3%);box-shadow:0 18px 55px rgba(22,75,59,.07);}
                                    .mwai-metric{position:relative;overflow:hidden;border-block-start:5px solid var(--mwai-sun);box-shadow:none;}
                                    .mwai-metric-body{display:flex;flex-direction:column;margin:0;}
                                    .mwai-metric dt{order:2;color:var(--mwai-muted);font-size:.82rem;font-weight:700;letter-spacing:.07em;text-transform:uppercase;}
                                    .mwai-metric dd{order:1;margin:0;color:var(--mwai-forest);font-size:clamp(2.2rem,5vw,4rem);font-weight:760;line-height:1;letter-spacing:-.045em;}
                                    .mwai-context{color:var(--mwai-muted);font-size:.95rem;}
                                    .mwai-callout{padding:0;}
                                    .mwai-callout aside{padding:clamp(1.5rem,3vw,2.5rem);border-radius:1.25rem;background:var(--mwai-forest);color:var(--mwai-paper);}
                                    .mwai-callout aside p:last-child{color:color-mix(in srgb,var(--mwai-paper),transparent 15%);}
                                    .mwai-eyebrow{margin:0 0 .65rem;color:var(--mwai-sun);font-size:.78rem;font-weight:750;letter-spacing:.11em;text-transform:uppercase;}
                                    figure{margin:0;}
                                    .mwai-chart figure,.mwai-asset figure{display:flex;flex-direction:column;gap:1rem;}
                                    .mwai-asset img{display:block;width:100%;height:auto;max-height:42rem;object-fit:contain;border-radius:.85rem;background:var(--mwai-mist);}
                                    figcaption{color:var(--mwai-muted);font-size:.92rem;line-height:1.55;}
                                    [data-mwai-chart]{width:100%;min-height:23rem;}
                                    .mwai-toolbar{display:flex;flex-wrap:wrap;gap:.75rem;align-items:center;}
                                    .mwai-table-wrap{overflow:auto;border:1px solid var(--mwai-line);border-radius:.85rem;}
                                    table{width:100%;border-collapse:separate;border-spacing:0;background:var(--mwai-paper);font-size:.92rem;}
                                    caption{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;}
                                    th,td{padding:.8rem 1rem;text-align:start;border-block-end:1px solid var(--mwai-line);vertical-align:top;}
                                    thead th{position:sticky;top:0;z-index:1;background:var(--mwai-forest);color:var(--mwai-paper);font-size:.78rem;letter-spacing:.05em;text-transform:uppercase;}
                                    tbody tr:nth-child(even){background:var(--mwai-mist);}
                                    tbody tr:last-child td{border-block-end:0;}
                                    select,input,button{font:inherit;}
                                    select,input[type="number"]{min-height:2.75rem;padding:.65rem .8rem;border:1px solid #AFC2B8;border-radius:.7rem;background:var(--mwai-paper);color:var(--mwai-ink);}
                                    input[type="range"]{min-height:2.75rem;accent-color:var(--mwai-pine);}
                                    button{min-height:2.75rem;padding:.6rem 1rem;border:1px solid var(--mwai-pine);border-radius:999px;background:var(--mwai-paper);color:var(--mwai-pine);font-weight:700;cursor:pointer;}
                                    button:hover{background:var(--mwai-mist);}
                                    button:focus-visible,select:focus-visible,input:focus-visible,summary:focus-visible{outline:3px solid var(--mwai-sun);outline-offset:3px;}
                                    [role="tablist"]{display:flex;flex-wrap:wrap;gap:.5rem;margin-block-end:1rem;border-block-end:1px solid var(--mwai-line);}
                                    [role="tab"]{border-color:transparent;border-radius:.65rem .65rem 0 0;}
                                    [role="tab"][aria-selected="true"]{background:var(--mwai-forest);color:var(--mwai-paper);}
                                    [role="tabpanel"]{padding:1rem 0;}
                                    details summary{cursor:pointer;font-weight:720;font-size:1.08rem;color:var(--mwai-forest);}
                                    .mwai-accordion-body{padding-block-start:1rem;color:var(--mwai-muted);}
                                    fieldset{margin:0;padding:0;border:0;}
                                    legend{padding:0;font-size:clamp(1.3rem,2.2vw,1.75rem);font-weight:720;letter-spacing:-.025em;color:var(--mwai-forest);}
                                    .mwai-control-grid{display:flex;flex-wrap:wrap;gap:1rem;margin-block:1.25rem;}
                                    .mwai-results{display:flex;flex-wrap:wrap;gap:.75rem;margin-block:1rem;}
                                    .mwai-results output{display:block;min-width:8rem;padding:1rem;border-radius:.8rem;background:var(--mwai-cream);color:var(--mwai-forest);font-size:1.45rem;font-weight:750;}
                                    .mwai-timeline-track{display:flex;flex-direction:column;list-style:none;margin:0;padding:0;padding-inline-start:.55rem;}
                                    .mwai-timeline-item{position:relative;min-width:0;padding:0;padding-block-end:1.75rem;padding-inline-start:1.75rem;border-inline-start:2px solid var(--mwai-line);}
                                    .mwai-timeline-item:last-child{padding-block-end:0;}
                                    .mwai-timeline-marker{position:absolute;inset-block-start:.18rem;inset-inline-start:-.52rem;width:.95rem;height:.95rem;border:3px solid var(--mwai-paper);border-radius:50%;background:var(--mwai-pine);box-shadow:0 0 0 2px var(--mwai-sage);}
                                    .mwai-timeline-content{display:flex;flex-direction:column;gap:.4rem;}
                                    .mwai-timeline-period,.mwai-timeline-description{margin:0;}
                                    .mwai-timeline-period{color:var(--mwai-pine);font-size:.78rem;font-weight:760;letter-spacing:.07em;text-transform:uppercase;}
                                    .mwai-timeline-content h4{margin:0;color:var(--mwai-forest);font-size:1.08rem;line-height:1.25;}
                                    .mwai-timeline-description{color:var(--mwai-muted);line-height:1.55;}
                                    .mwai-sources{display:block;padding-block-start:.8rem;border-block-start:1px solid var(--mwai-line);color:var(--mwai-muted);font-size:.76rem;line-height:1.5;}
                                    .mwai-emphasized{border-color:var(--mwai-sun);box-shadow:0 18px 55px rgba(22,75,59,.12);}
                                    .mwai-align-start{align-items:start;}.mwai-align-center{align-items:center;}.mwai-align-end{align-items:end;}.mwai-align-stretch{align-items:stretch;}
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
        
        css.Append("""
                   @media screen and (min-width:48rem){.mwai-timeline-horizontal .mwai-timeline-track{display:grid;grid-auto-flow:column;grid-auto-columns:minmax(13rem,1fr);flex-shrink:0;overflow-x:auto;padding:.55rem 0 .5rem;padding-inline-start:.55rem}.mwai-timeline-horizontal .mwai-timeline-item{padding:0;padding-block-start:1.5rem;padding-inline-end:1rem;border-block-start:2px solid var(--mwai-line);border-inline-start:0}.mwai-timeline-horizontal .mwai-timeline-marker{inset-block-start:-.52rem;inset-inline-start:-.52rem}}
                   @media(max-width:47.99rem){#mwai-briefing-root{padding:1rem .75rem .75rem}.mwai-section-inner{padding:1.5rem}.mwai-section-hero .mwai-section-inner{min-height:34rem}.mwai-metric,.mwai-chart,.mwai-asset,.mwai-table,.mwai-filterable_table,.mwai-tabs,.mwai-accordion,.mwai-simulation,.mwai-timeline{padding:1rem}[data-mwai-chart]{min-height:19rem}th,td{padding:.7rem .75rem}}
                   @media print{@page{margin:14mm}#mwai-briefing-root{max-width:none;padding:0;font-size:10pt}.mwai-document{gap:8mm}.mwai-section{border:0;box-shadow:none;background:transparent;color:var(--mwai-ink);break-inside:auto}.mwai-section-inner{padding:6mm 0}.mwai-section-heading{margin-block-end:5mm}.mwai-section-heading h1{font-size:28pt}.mwai-section-heading h2{font-size:21pt}.mwai-section-heading p,.mwai-section-hero .mwai-section-heading p,.mwai-section-conclusion .mwai-section-heading p{color:var(--mwai-muted)}.mwai-component,.mwai-component figure,.mwai-table-wrap{break-inside:avoid}.mwai-timeline{break-inside:auto}.mwai-timeline-item{break-inside:avoid}.mwai-metric,.mwai-chart,.mwai-asset,.mwai-table,.mwai-filterable_table,.mwai-tabs,.mwai-accordion,.mwai-simulation,.mwai-timeline{box-shadow:none;background:var(--mwai-paper)}[data-mwai-tab-panel][hidden]{display:block!important}details:not([open])>.mwai-accordion-body{display:block!important}[data-mwai-reset]{display:none!important}thead th{position:static}*{print-color-adjust:exact}}
                   """);
        
        return css.ToString();
    }

    private static IEnumerable<VisualBriefingLayoutNode> EnumerateGridNodes(VisualBriefingLayoutNode node)
    {
        if (node.Kind is VisualBriefingLayoutNodeKind.GRID)
            yield return node;

        foreach (var grid in node.Children.SelectMany(EnumerateGridNodes))
            yield return grid;
    }
}