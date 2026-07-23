using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Components;

using MudBlazor;

namespace AIStudio.Components;

public partial class JsonTreeView : ComponentBase
{
    [Parameter]
    public JsonNode? Value { get; set; }

    private IReadOnlyCollection<TreeItemData<JsonTreeNode>> items = [];

    protected override void OnParametersSet()
    {
        this.items = [CreateTreeItem("$", this.Value)];
    }

    private static TreeItemData<JsonTreeNode> CreateTreeItem(string label, JsonNode? value)
    {
        var children = CreateChildren(value);
        return new TreeItemData<JsonTreeNode>
        {
            Expanded = false,
            Expandable = children.Count > 0,
            Value = new JsonTreeNode
            {
                Text = $"{label}: {FormatValue(value)}",
                Icon = GetIcon(value),
                Expandable = children.Count > 0,
            },
            Children = children,
        };
    }

    private static List<TreeItemData<JsonTreeNode>> CreateChildren(JsonNode? value) => value switch
    {
        JsonObject jsonObject => jsonObject
            .Select(property => CreateTreeItem(JsonSerializer.Serialize(property.Key), property.Value))
            .ToList(),
        JsonArray jsonArray => jsonArray
            .Select((item, index) => CreateTreeItem($"[{index}]", item))
            .ToList(),
        _ => [],
    };

    private static string FormatValue(JsonNode? value) => value switch
    {
        JsonObject jsonObject when jsonObject.Count == 0 => "{}",
        JsonObject => "{...}",
        JsonArray jsonArray when jsonArray.Count == 0 => "[]",
        JsonArray => "[...]",
        null => "null",
        _ => value.ToJsonString(),
    };

    private static string GetIcon(JsonNode? value) => value switch
    {
        JsonObject => Icons.Material.Filled.DataObject,
        JsonArray => Icons.Material.Filled.DataArray,
        _ => Icons.Material.Filled.Code,
    };

    private sealed class JsonTreeNode
    {
        public string Text { get; init; } = string.Empty;

        public string Icon { get; init; } = string.Empty;

        public bool Expandable { get; init; }
    }
}
