using AIStudio.Assistants.Dynamic;
using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public sealed class AssistantState
{
    public readonly Dictionary<string, string> Text = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> SingleSelect = new(StringComparer.Ordinal);
    public readonly Dictionary<string, HashSet<string>> MultiSelect = new(StringComparer.Ordinal);
    public readonly Dictionary<string, bool> Booleans = new(StringComparer.Ordinal);
    public readonly Dictionary<string, WebContentState> WebContent = new(StringComparer.Ordinal);
    public readonly Dictionary<string, FileContentState> FileContent = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Colors = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Dates = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> DateRanges = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> Times = new(StringComparer.Ordinal);

    public void Clear()
    {
        this.Text.Clear();
        this.SingleSelect.Clear();
        this.MultiSelect.Clear();
        this.Booleans.Clear();
        this.WebContent.Clear();
        this.FileContent.Clear();
        this.Colors.Clear();
        this.Dates.Clear();
        this.DateRanges.Clear();
        this.Times.Clear();
    }

    public bool TryApplyValue(string fieldName, LuaValue value, out string expectedType)
    {
        expectedType = string.Empty;

        if (this.Text.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var textValue))
                return false;

            this.Text[fieldName] = textValue;
            return true;
        }

        if (this.SingleSelect.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var singleSelectValue))
                return false;

            this.SingleSelect[fieldName] = singleSelectValue;
            return true;
        }

        if (this.MultiSelect.ContainsKey(fieldName))
        {
            expectedType = "string[]";
            if (value.TryRead<LuaTable>(out var multiselectTable))
            {
                this.MultiSelect[fieldName] = ReadStringValues(multiselectTable);
                return true;
            }

            if (!value.TryRead<string>(out var singleValue))
                return false;

            this.MultiSelect[fieldName] = string.IsNullOrWhiteSpace(singleValue) ? [] : [singleValue];
            return true;
        }

        if (this.Booleans.ContainsKey(fieldName))
        {
            expectedType = "boolean";
            if (!value.TryRead<bool>(out var boolValue))
                return false;

            this.Booleans[fieldName] = boolValue;
            return true;
        }

        if (this.WebContent.TryGetValue(fieldName, out var webContentState))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var webContentValue))
                return false;

            webContentState.Content = webContentValue;
            return true;
        }

        if (this.FileContent.TryGetValue(fieldName, out var fileContentState))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var fileContentValue))
                return false;

            fileContentState.Content = fileContentValue;
            return true;
        }

        if (this.Colors.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var colorValue))
                return false;

            this.Colors[fieldName] = colorValue;
            return true;
        }

        if (this.Dates.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var dateValue))
                return false;

            this.Dates[fieldName] = dateValue;
            return true;
        }

        if (this.DateRanges.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var dateRangeValue))
                return false;

            this.DateRanges[fieldName] = dateRangeValue;
            return true;
        }

        if (this.Times.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var timeValue))
                return false;

            this.Times[fieldName] = timeValue;
            return true;
        }

        return false;
    }

    public LuaTable ToLuaTable(IEnumerable<IAssistantComponent> components)
    {
        var table = new LuaTable();
        this.AddEntries(table, components);
        return table;
    }

    private void AddEntries(LuaTable target, IEnumerable<IAssistantComponent> components)
    {
        foreach (var component in components)
        {
            if (component is INamedAssistantComponent named)
            {
                var componentEntry = new LuaTable
                {
                    ["Type"] = Enum.GetName(component.Type) ?? string.Empty,
                    ["Value"] = component is IStatefulAssistantComponent ? this.ReadValueForLua(named.Name) : LuaValue.Nil,
                    ["Props"] = this.CreatePropsTable(component),
                };

                if (component is AssistantDropdown dropdown)
                    this.AddDropdownDisplay(componentEntry, dropdown, named.Name);

                target[named.Name] = componentEntry;
            }

            if (component.Children.Count > 0)
                this.AddEntries(target, component.Children);
        }
    }

    private LuaValue ReadValueForLua(string name)
    {
        if (this.Text.TryGetValue(name, out var textValue))
            return textValue;
        if (this.SingleSelect.TryGetValue(name, out var singleSelectValue))
            return singleSelectValue;
        if (this.MultiSelect.TryGetValue(name, out var multiSelectValue))
            return AssistantLuaConversion.CreateLuaArray(multiSelectValue.OrderBy(static value => value, StringComparer.Ordinal));
        if (this.Booleans.TryGetValue(name, out var boolValue))
            return boolValue;
        if (this.WebContent.TryGetValue(name, out var webContentValue))
            return webContentValue.Content;
        if (this.FileContent.TryGetValue(name, out var fileContentValue))
            return fileContentValue.Content;
        if (this.Colors.TryGetValue(name, out var colorValue))
            return colorValue;
        if (this.Dates.TryGetValue(name, out var dateValue))
            return dateValue;
        if (this.DateRanges.TryGetValue(name, out var dateRangeValue))
            return dateRangeValue;
        if (this.Times.TryGetValue(name, out var timeValue))
            return timeValue;

        return LuaValue.Nil;
    }

    private LuaTable CreatePropsTable(IAssistantComponent component)
    {
        var table = new LuaTable();
        var nonReadableProps = ComponentPropSpecs.SPECS.TryGetValue(component.Type, out var propSpec)
            ? propSpec.NonReadable
            : [];

        foreach (var key in component.Props.Keys)
        {
            if (nonReadableProps.Contains(key, StringComparer.Ordinal))
                continue;

            if (!component.Props.TryGetValue(key, out var value))
                continue;

            if (!AssistantLuaConversion.TryWriteAssistantValue(table, key, value))
                // ReSharper disable once RedundantJumpStatement
                continue;
        }

        return table;
    }

    private void AddDropdownDisplay(LuaTable componentEntry, AssistantDropdown dropdown, string name)
    {
        if (dropdown.IsMultiselect)
        {
            if (!this.MultiSelect.TryGetValue(name, out var selectedValues))
                return;

            componentEntry["Display"] = AssistantLuaConversion.CreateLuaArray(
                selectedValues
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .Select(dropdown.ResolveDisplayText));

            return;
        }

        if (!this.SingleSelect.TryGetValue(name, out var selectedValue))
            return;

        componentEntry["Display"] = dropdown.ResolveDisplayText(selectedValue);
    }

    private static HashSet<string> ReadStringValues(LuaTable values)
    {
        var parsedValues = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in values)
        {
            if (entry.Value.TryRead<string>(out var value) && !string.IsNullOrWhiteSpace(value))
                parsedValues.Add(value);
        }

        return parsedValues;
    }
}
