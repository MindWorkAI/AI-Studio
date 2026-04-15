using System.Collections;
using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

internal static class AssistantLuaConversion
{
    /// <summary>
    /// Converts a sequence of scalar .NET values into the array-like Lua table shape used by assistant state.
    /// </summary>
    public static LuaTable CreateLuaArray(IEnumerable values) => CreateLuaArrayCore(values);

    /// <summary>
    /// Creates a readable string representation of a Lua table for debugging and inspection.
    /// </summary>
    public static string InspectTable(LuaTable table) => InspectTableCore(table, 0);

    /// <summary>
    /// Reads a Lua value into either a scalar .NET value or one of the structured assistant data model types.
    /// Lua itself only exposes scalars and tables, so structured assistant types such as dropdown/list items
    /// must be detected from well-known table shapes.
    /// </summary>
    public static bool TryReadScalarOrStructuredValue(LuaValue value, out object result)
    {
        if (value.TryRead<string>(out var stringValue))
        {
            result = stringValue;
            return true;
        }

        if (value.TryRead<bool>(out var boolValue))
        {
            result = boolValue;
            return true;
        }

        if (value.TryRead<double>(out var doubleValue))
        {
            result = doubleValue;
            return true;
        }

        if (value.TryRead<LuaTable>(out var table) && TryParseDropdownItem(table, out var dropdownItem))
        {
            result = dropdownItem;
            return true;
        }

        if (value.TryRead<LuaTable>(out var dropdownListTable) && TryParseDropdownItemList(dropdownListTable, out var dropdownItems))
        {
            result = dropdownItems;
            return true;
        }

        if (value.TryRead<LuaTable>(out var listItemListTable) && TryParseListItemList(listItemListTable, out var listItems))
        {
            result = listItems;
            return true;
        }

        result = null!;
        return false;
    }

    /// <summary>
    /// Writes an assistant value into a Lua table.
    /// This supports a broader set of .NET types than <see cref="TryReadScalarOrStructuredValue"/>,
    /// because assistant props and state already exist as rich C# objects before being serialized back to Lua.
    /// </summary>
    public static bool TryWriteAssistantValue(LuaTable table, string key, object? value)
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
                table[key] = boolValue;
                return true;
            case byte byteValue:
                table[key] = byteValue;
                return true;
            case sbyte sbyteValue:
                table[key] = sbyteValue;
                return true;
            case short shortValue:
                table[key] = shortValue;
                return true;
            case ushort ushortValue:
                table[key] = ushortValue;
                return true;
            case int intValue:
                table[key] = intValue;
                return true;
            case uint uintValue:
                table[key] = uintValue;
                return true;
            case long longValue:
                table[key] = longValue;
                return true;
            case ulong ulongValue:
                table[key] = ulongValue;
                return true;
            case float floatValue:
                table[key] = floatValue;
                return true;
            case double doubleValue:
                table[key] = doubleValue;
                return true;
            case decimal decimalValue:
                table[key] = (double)decimalValue;
                return true;
            case Enum enumValue:
                table[key] = enumValue.ToString();
                return true;
            case AssistantDropdownItem dropdownItem:
                table[key] = CreateDropdownItemTable(dropdownItem);
                return true;
            case IEnumerable<AssistantDropdownItem> dropdownItems:
                table[key] = CreateLuaArrayCore(dropdownItems.Select(CreateDropdownItemTable));
                return true;
            case IEnumerable<AssistantListItem> listItems:
                table[key] = CreateLuaArrayCore(listItems.Select(CreateListItemTable));
                return true;
            case IEnumerable<string> strings:
                table[key] = CreateLuaArrayCore(strings);
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseDropdownItem(LuaTable table, out AssistantDropdownItem item)
    {
        item = new AssistantDropdownItem();

        if (!table.TryGetValue("Value", out var valueValue) || !valueValue.TryRead<string>(out var value))
            return false;

        if (!table.TryGetValue("Display", out var displayValue) || !displayValue.TryRead<string>(out var display))
            return false;

        item.Value = value;
        item.Display = display;
        return true;
    }

    private static bool TryParseDropdownItemList(LuaTable table, out List<AssistantDropdownItem> items)
    {
        items = new List<AssistantDropdownItem>();

        for (var index = 1; index <= table.ArrayLength; index++)
        {
            var value = table[index];
            if (!value.TryRead<LuaTable>(out var itemTable) || !TryParseDropdownItem(itemTable, out var item))
            {
                items = null!;
                return false;
            }

            items.Add(item);
        }

        return true;
    }

    private static bool TryParseListItem(LuaTable table, out AssistantListItem item)
    {
        item = new AssistantListItem();

        if (!table.TryGetValue("Text", out var textValue) || !textValue.TryRead<string>(out var text))
            return false;

        if (!table.TryGetValue("Type", out var typeValue) || !typeValue.TryRead<string>(out var type))
            return false;

        table.TryGetValue("Icon", out var iconValue);
        iconValue.TryRead<string>(out var icon);

        table.TryGetValue("IconColor", out var iconColorValue);
        iconColorValue.TryRead<string>(out var iconColor);

        item.Text = text;
        item.Type = type;
        item.Icon = icon;
        item.IconColor = iconColor;

        if (table.TryGetValue("Href", out var hrefValue) && hrefValue.TryRead<string>(out var href))
            item.Href = href;

        return true;
    }

    private static bool TryParseListItemList(LuaTable table, out List<AssistantListItem> items)
    {
        items = new List<AssistantListItem>();

        for (var index = 1; index <= table.ArrayLength; index++)
        {
            var value = table[index];
            if (!value.TryRead<LuaTable>(out var itemTable) || !TryParseListItem(itemTable, out var item))
            {
                items = null!;
                return false;
            }

            items.Add(item);
        }

        return true;
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

    private static LuaTable CreateLuaArrayCore(IEnumerable values)
    {
        var luaArray = new LuaTable();
        var index = 1;

        foreach (var value in values)
        {
            luaArray[index++] = value switch
            {
                null => LuaValue.Nil,
                LuaValue luaValue => luaValue,
                LuaTable luaTable => luaTable,
                string stringValue => (LuaValue)stringValue,
                bool boolValue => boolValue,
                byte byteValue => byteValue,
                sbyte sbyteValue => sbyteValue,
                short shortValue => shortValue,
                ushort ushortValue => ushortValue,
                int intValue => intValue,
                uint uintValue => uintValue,
                long longValue => longValue,
                ulong ulongValue => ulongValue,
                float floatValue => floatValue,
                double doubleValue => doubleValue,
                decimal decimalValue => (double)decimalValue,
                _ => LuaValue.Nil,
            };
        }

        return luaArray;
    }

    private static string InspectTableCore(LuaTable table, int depth)
    {
        if (depth > 8)
            return "{ ... }";

        var indent = new string(' ', depth * 2);
        var childIndent = new string(' ', (depth + 1) * 2);
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("{");

        foreach (var entry in table)
        {
            builder.Append(childIndent);
            builder.Append(FormatLuaValue(entry.Key));
            builder.Append(" = ");
            builder.AppendLine(FormatLuaValue(entry.Value, depth + 1));
        }

        builder.Append(indent);
        builder.Append('}');
        return builder.ToString();
    }

    private static string FormatLuaValue(LuaValue value, int depth = 0)
    {
        if (value.Type is LuaValueType.Nil)
            return "nil";

        if (value.TryRead<string>(out var stringValue))
            return $"\"{stringValue.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        if (value.TryRead<bool>(out var boolValue))
            return boolValue ? "true" : "false";

        if (value.TryRead<double>(out var doubleValue))
            return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (value.TryRead<LuaTable>(out var tableValue))
            return InspectTableCore(tableValue, depth);

        return value.ToString();
    }
}
