using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIStudio.Tools.PluginSystem.Assistants.DataModel;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AIStudio.Components
{
    public partial class DynamicAssistantDropdown : ComponentBase
    {
        [Parameter]
        public List<AssistantDropdownItem> Items { get; set; } = new();

        [Parameter]
        public AssistantDropdownItem Default { get; set; } = new();

        [Parameter]
        public string Value { get; set; } = string.Empty;

        [Parameter]
        public EventCallback<string> ValueChanged { get; set; }

        [Parameter]
        public HashSet<string> SelectedValues { get; set; } = [];

        [Parameter]
        public EventCallback<HashSet<string>> SelectedValuesChanged { get; set; }

        [Parameter]
        public string Label { get; set; } = string.Empty;
        
        [Parameter]
        public string HelperText { get; set; } = string.Empty;

        [Parameter]
        public Func<string, string?> ValidateSelection { get; set; } = _ => null;

        [Parameter]
        public string OpenIcon { get; set; } = Icons.Material.Filled.ArrowDropDown;
        
        [Parameter]
        public string CloseIcon { get; set; } = Icons.Material.Filled.ArrowDropUp;
        
        [Parameter]
        public Color IconColor { get; set; } = Color.Default;

        [Parameter]
        public Adornment IconPosition { get; set; } = Adornment.End;
        
        [Parameter]
        public Variant Variant { get; set; } = Variant.Outlined;
        
        [Parameter]
        public bool IsMultiselect { get; set; }
        
        [Parameter]
        public bool HasSelectAll { get; set; }
        
        [Parameter]
        public string SelectAllText { get; set; } = string.Empty;

        [Parameter]
        public string Class { get; set; } = string.Empty;
        
        [Parameter]
        public string Style { get; set; } = string.Empty;

        private async Task OnValueChanged(string newValue)
        {
            if (this.Value != newValue)
            {
                this.Value = newValue;
                await this.ValueChanged.InvokeAsync(newValue);
            }
        }

        private async Task OnSelectedValuesChanged(IEnumerable<string?>? newValues)
        {
            var updatedValues = newValues?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.Ordinal) ?? [];

            if (this.SelectedValues.SetEquals(updatedValues))
                return;

            this.SelectedValues = updatedValues;
            await this.SelectedValuesChanged.InvokeAsync(updatedValues);
        }

        private List<AssistantDropdownItem> GetRenderedItems()
        {
            var items = this.Items ?? [];
            if (string.IsNullOrWhiteSpace(this.Default.Value))
                return items;

            if (items.Any(item => string.Equals(item.Value, this.Default.Value, StringComparison.Ordinal)))
                return items;

            return [this.Default, .. items];
        }

        private string GetMultiSelectionText(List<string?>? selectedValues)
        {
            if (selectedValues is null || selectedValues.Count == 0)
                return this.Default.Display;

            var labels = selectedValues
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => this.ResolveDisplayText(value!))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            return labels.Count == 0 ? this.Default.Display : string.Join(", ", labels);
        }

        private string ResolveDisplayText(string value)
        {
            var item = this.GetRenderedItems().FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.Ordinal));
            return item?.Display ?? value;
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
