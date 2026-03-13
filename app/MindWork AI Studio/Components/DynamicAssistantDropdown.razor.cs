using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AIStudio.Components
{
    public partial class DynamicAssistantDropdown : ComponentBase
    {
        [Parameter] public List<AssistantDropdownItem> Items { get; set; } = new();

        [Parameter] public AssistantDropdownItem Default { get; set; } = new();

        [Parameter] public string Value { get; set; } = string.Empty;

        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        [Parameter] public string Label { get; set; } = string.Empty;
        
        [Parameter] public string HelperText { get; set; } = string.Empty;

        [Parameter] public Func<string, string?> ValidateSelection { get; set; } = _ => null;

        [Parameter] public string OpenIcon { get; set; } = Icons.Material.Filled.ArrowDropDown;
        
        [Parameter] public string CloseIcon { get; set; } = Icons.Material.Filled.ArrowDropUp;
        
        [Parameter] public Color IconColor { get; set; } = Color.Default;

        [Parameter] public Adornment IconPosition { get; set; } = Adornment.End;
        
        [Parameter] public Variant Variant { get; set; } = Variant.Outlined;
        
        [Parameter] public bool IsMultiselect { get; set; }
        
        [Parameter] public bool HasSelectAll { get; set; }
        
        [Parameter] public string SelectAllText { get; set; } = string.Empty;

        [Parameter] public string Class { get; set; } = string.Empty;
        
        [Parameter] public string Style { get; set; } = string.Empty;

        private async Task OnValueChanged(string newValue)
        {
            if (this.Value != newValue)
            {
                this.Value = newValue;
                await this.ValueChanged.InvokeAsync(newValue);
            }
        }

        private string MergeClasses(string custom, string fallback)
        {
            var trimmedCustom = custom?.Trim() ?? string.Empty;
            var trimmedFallback = fallback?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmedCustom))
                return trimmedFallback;

            return string.IsNullOrEmpty(trimmedFallback) ? trimmedCustom : $"{trimmedCustom} {trimmedFallback}";
        }
    }
}