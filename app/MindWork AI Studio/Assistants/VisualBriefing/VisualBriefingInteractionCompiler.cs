using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AIStudio.Assistants.VisualBriefing;

/// <summary>
/// Compiles interaction state and safe declarative controls.
/// </summary>
internal static class VisualBriefingInteractionCompiler
{
    /// <summary>
    /// Compiles controls and formulas into deterministic runtime state.
    /// </summary>
    /// <param name="controls">The validated interaction controls.</param>
    /// <param name="formulas">The validated formula specifications.</param>
    /// <returns>The declarative interaction data.</returns>
    internal static JsonElement Compile(IReadOnlyList<VisualBriefingControlSpec> controls, IReadOnlyList<VisualBriefingFormulaSpec> formulas)
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
        }, VisualBriefingJson.Canonical);
    }

    /// <summary>
    /// Compiles safe control markup for one component.
    /// </summary>
    /// <param name="componentId">The owning component identifier.</param>
    /// <param name="controls">All validated briefing controls.</param>
    /// <returns>The declarative control markup.</returns>
    internal static string CompileMarkup(string componentId, IReadOnlyList<VisualBriefingControlSpec> controls)
    {
        var builder = new StringBuilder();
        foreach (var indexed in controls.Select((control, index) => (Control: control, Index: index)).Where(item => item.Control.ComponentId == componentId))
        {
            var control = indexed.Control;
            var id = HtmlEncoder.Default.Encode(control.ControlId);
            var accessibilityPath = $"accessibility.{HtmlEncoder.Default.Encode(componentId)}";
            
            builder.Append(control.Kind switch
            {
                VisualBriefingControlKind.SELECT or VisualBriefingControlKind.FILTER => $"<select data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\"><template data-mwai-each=\"interactions.controls.{indexed.Index}.options\"><option data-mwai-attr-value=\".value\" data-mwai-text=\".label\"></option></template></select>",
                VisualBriefingControlKind.RANGE => $"<input type=\"range\" data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\">",
                VisualBriefingControlKind.NUMBER => $"<input type=\"number\" data-mwai-model=\"interactions.state.{id}\" data-mwai-attr-aria-label=\"{accessibilityPath}\">",
                _ => string.Empty,
            });
        }

        return builder.ToString();
    }

    /// <summary>
    /// Compiles a deterministic reset action for one simulation component.
    /// </summary>
    /// <param name="componentId">The simulation component identifier.</param>
    /// <returns>The declarative reset button markup.</returns>
    internal static string CompileResetMarkup(string componentId) => $"<button type=\"button\" data-mwai-reset=\"{HtmlEncoder.Default.Encode(componentId)}\" data-mwai-text=\"labels.reset\"></button>";
}