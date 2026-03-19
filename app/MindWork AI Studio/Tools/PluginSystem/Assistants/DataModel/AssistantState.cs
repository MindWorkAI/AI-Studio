using System.Collections;
using AIStudio.Assistants.Dynamic;
using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public sealed class AssistantState
{
    public readonly Dictionary<string, string> Text = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> SingleSelect = new(StringComparer.Ordinal);
    public readonly Dictionary<string, HashSet<string>> MultiSelect = new(StringComparer.Ordinal);
    public readonly Dictionary<string, bool> Bools = new(StringComparer.Ordinal);
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
        this.Bools.Clear();
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

            this.Text[fieldName] = textValue ?? string.Empty;
            return true;
        }

        if (this.SingleSelect.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var singleSelectValue))
                return false;

            this.SingleSelect[fieldName] = singleSelectValue ?? string.Empty;
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

        if (this.Bools.ContainsKey(fieldName))
        {
            expectedType = "boolean";
            if (!value.TryRead<bool>(out var boolValue))
                return false;

            this.Bools[fieldName] = boolValue;
            return true;
        }

        if (this.WebContent.TryGetValue(fieldName, out var webContentState))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var webContentValue))
                return false;

            webContentState.Content = webContentValue ?? string.Empty;
            return true;
        }

        if (this.FileContent.TryGetValue(fieldName, out var fileContentState))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var fileContentValue))
                return false;

            fileContentState.Content = fileContentValue ?? string.Empty;
            return true;
        }

        if (this.Colors.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var colorValue))
                return false;

            this.Colors[fieldName] = colorValue ?? string.Empty;
            return true;
        }

        if (this.Dates.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var dateValue))
                return false;

            this.Dates[fieldName] = dateValue ?? string.Empty;
            return true;
        }

        if (this.DateRanges.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var dateRangeValue))
                return false;

            this.DateRanges[fieldName] = dateRangeValue ?? string.Empty;
            return true;
        }

        if (this.Times.ContainsKey(fieldName))
        {
            expectedType = "string";
            if (!value.TryRead<string>(out var timeValue))
                return false;

            this.Times[fieldName] = timeValue ?? string.Empty;
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
                target[named.Name] = new LuaTable
                {
                    ["Value"] = component is IStatefulAssistantComponent ? this.ReadValueForLua(named.Name) : LuaValue.Nil,
                    ["Props"] = this.CreatePropsTable(component),
                };
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
            return CreateLuaArray(multiSelectValue.OrderBy(static value => value, StringComparer.Ordinal));
        if (this.Bools.TryGetValue(name, out var boolValue))
            return boolValue;
        if (this.WebContent.TryGetValue(name, out var webContentValue))
            return webContentValue.Content ?? string.Empty;
        if (this.FileContent.TryGetValue(name, out var fileContentValue))
            return fileContentValue.Content ?? string.Empty;
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

            if (!TryWriteLuaValue(table, key, value))
                continue;
        }

        return table;
    }

    private static bool TryWriteLuaValue(LuaTable table, string key, object? value)
    {
        if (value is null or LuaFunction)
            return false;

        switch (value)
        {
            case LuaValue { Type: not LuaValueType.Nil } luaValue:
                table[key] = luaValue;
                return true;
            case LuaTable luaTable:
                table[key] = luaTable;
                return true;
            case string stringValue:
                table[key] = (LuaValue)stringValue;
                return true;
            case bool boolValue:
                table[key] = (LuaValue)boolValue;
                return true;
            case byte byteValue:
                table[key] = (LuaValue)byteValue;
                return true;
            case sbyte sbyteValue:
                table[key] = (LuaValue)sbyteValue;
                return true;
            case short shortValue:
                table[key] = (LuaValue)shortValue;
                return true;
            case ushort ushortValue:
                table[key] = (LuaValue)ushortValue;
                return true;
            case int intValue:
                table[key] = (LuaValue)intValue;
                return true;
            case uint uintValue:
                table[key] = (LuaValue)uintValue;
                return true;
            case long longValue:
                table[key] = (LuaValue)longValue;
                return true;
            case ulong ulongValue:
                table[key] = (LuaValue)ulongValue;
                return true;
            case float floatValue:
                table[key] = (LuaValue)floatValue;
                return true;
            case double doubleValue:
                table[key] = (LuaValue)doubleValue;
                return true;
            case decimal decimalValue:
                table[key] = (LuaValue)(double)decimalValue;
                return true;
            case Enum enumValue:
                table[key] = enumValue.ToString() ?? string.Empty;
                return true;
            case AssistantDropdownItem dropdownItem:
                table[key] = CreateDropdownItemTable(dropdownItem);
                return true;
            case IEnumerable<AssistantDropdownItem> dropdownItems:
                table[key] = CreateLuaArray(dropdownItems.Select(CreateDropdownItemTable));
                return true;
            case IEnumerable<AssistantListItem> listItems:
                table[key] = CreateLuaArray(listItems.Select(CreateListItemTable));
                return true;
            case IEnumerable<string> strings:
                table[key] = CreateLuaArray(strings);
                return true;
            default:
                return false;
        }
    }

    private static LuaTable CreateDropdownItemTable(AssistantDropdownItem item) =>
        new()
        {
            ["Value"] = item.Value,
            ["Display"] = item.Display,
        };

    private static LuaTable CreateListItemTable(AssistantListItem item)
    {
        var table = new LuaTable
        {
            ["Type"] = item.Type,
            ["Text"] = item.Text,
            ["Icon"] = item.Icon,
            ["IconColor"] = item.IconColor,
        };

        if (!string.IsNullOrWhiteSpace(item.Href))
            table["Href"] = item.Href;

        return table;
    }

    private static LuaTable CreateLuaArray(IEnumerable values)
    {
        var luaArray = new LuaTable();
        var index = 1;

        foreach (var value in values)
            luaArray[index++] = value switch
            {
                null => LuaValue.Nil,
                LuaValue luaValue => luaValue,
                LuaTable luaTable => luaTable,
                string stringValue => (LuaValue)stringValue,
                bool boolValue => (LuaValue)boolValue,
                byte byteValue => (LuaValue)byteValue,
                sbyte sbyteValue => (LuaValue)sbyteValue,
                short shortValue => (LuaValue)shortValue,
                ushort ushortValue => (LuaValue)ushortValue,
                int intValue => (LuaValue)intValue,
                uint uintValue => (LuaValue)uintValue,
                long longValue => (LuaValue)longValue,
                ulong ulongValue => (LuaValue)ulongValue,
                float floatValue => (LuaValue)floatValue,
                double doubleValue => (LuaValue)doubleValue,
                decimal decimalValue => (LuaValue)(double)decimalValue,
                _ => LuaValue.Nil,
            };

        return luaArray;
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
