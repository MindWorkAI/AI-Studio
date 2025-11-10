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
        [Parameter]
        public List<AssistantDropdownItem> Items { get; set; } = new();
        
        [Parameter]
        public AssistantDropdownItem Default { get; set; } = new();

        [Parameter]
        public string Value { get; set; } = string.Empty;

        [Parameter]
        public EventCallback<string> ValueChanged { get; set; }

        [Parameter]
        public string Label { get; set; } = string.Empty;

        [Parameter]
        public Func<string, string?> ValidateSelection { get; set; } = _ => null;

        [Parameter]
        public string Icon { get; set; } = Icons.Material.Filled.ArrowDropDown;
    }
}