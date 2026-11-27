using AIStudio.Assistants.Dynamic;
using AIStudio.Chat;
using AIStudio.Tools.AssistantSessions;

using Lua;

namespace AIStudio.Tools.PluginSystem.Assistants.DataModel;

public sealed class AssistantState : IAssistantSessionValue
{
    public readonly Dictionary<string, string> Text = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> SingleSelect = new(StringComparer.Ordinal);
    public readonly Dictionary<string, HashSet<string>> MultiSelect = new(StringComparer.Ordinal);
    public readonly Dictionary<string, bool> Booleans = new(StringComparer.Ordinal);
    public readonly Dictionary<string, WebContentState> WebContent = new(StringComparer.Ordinal);
    public readonly Dictionary<string, FileContentState> FileContent = new(StringComparer.Ordinal);
    public readonly Dictionary<string, FileAttachmentState> FileAttachments = new(StringComparer.Ordinal);
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
        this.FileAttachments.Clear();
        this.Colors.Clear();
        this.Dates.Clear();
        this.DateRanges.Clear();
        this.Times.Clear();
    }

    /// <summary>
    /// Copies all dynamic assistant state values from another state instance.
    /// </summary>
    /// <remarks>
    /// The copy shares nothing with the other state, not even the states of web content, file content, or
    /// attachments, which the form changes in place. A snapshot of an assistant session holds such a copy, and
    /// it must neither change along with the form nor look unchanged when the form changed one of them.
    /// </remarks>
    /// <param name="other">The state instance to copy from.</param>
    public void CopyFrom(AssistantState other)
    {
        this.Clear();
        CopyDictionary(other.Text, this.Text);
        CopyDictionary(other.SingleSelect, this.SingleSelect);
        CopyDictionary(other.MultiSelect, this.MultiSelect, values => new HashSet<string>(values, values.Comparer));
        CopyDictionary(other.Booleans, this.Booleans);
        CopyDictionary(other.WebContent, this.WebContent, state => state.Copy());
        CopyDictionary(other.FileContent, this.FileContent, state => state.Copy());
        CopyDictionary(other.FileAttachments, this.FileAttachments, state => state.Copy());
        CopyDictionary(other.Colors, this.Colors);
        CopyDictionary(other.Dates, this.Dates);
        CopyDictionary(other.DateRanges, this.DateRanges);
        CopyDictionary(other.Times, this.Times);
    }

    /// <summary>
    /// Creates a copy of the dynamic assistant state.
    /// </summary>
    /// <returns>A copied assistant state instance.</returns>
    public AssistantState Clone()
    {
        var clone = new AssistantState();
        clone.CopyFrom(this);
        return clone;
    }

    /// <summary>
    /// Clears the flags of work in progress, such as a content cleaner which runs for a web page.
    /// </summary>
    /// <remarks>
    /// Such work ends with the component which started it. A draft restores the whole state, though, and a
    /// flag restored from it would stay set and keep its field locked for good. The built-in assistants keep
    /// such flags out of drafts by their keys, see AssistantSessionStateKey.IsKeptOutOfDrafts; the flags of an
    /// assistant plugin live inside this state instead.
    /// </remarks>
    public void ClearFlagsOfWorkInProgress()
    {
        foreach (var webContent in this.WebContent.Values)
            webContent.CleanerIsRunning = false;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Compares the values of all fields. Whether a web content cleaner runs is not an input and does not count,
    /// see WebContentState.HasSameInputsAs.
    /// </remarks>
    public bool HasSameValueAs(object other) => other is AssistantState otherState
                                                && HaveSameEntries(this.Text, otherState.Text, IsSameText)
                                                && HaveSameEntries(this.SingleSelect, otherState.SingleSelect, IsSameText)
                                                && HaveSameEntries(this.MultiSelect, otherState.MultiSelect, (first, second) => first.SetEquals(second))
                                                && HaveSameEntries(this.Booleans, otherState.Booleans, (first, second) => first == second)
                                                && HaveSameEntries(this.WebContent, otherState.WebContent, (first, second) => first.HasSameInputsAs(second))
                                                && HaveSameEntries(this.FileContent, otherState.FileContent, (first, second) => first.HasSameInputsAs(second))
                                                && HaveSameEntries(this.FileAttachments, otherState.FileAttachments, (first, second) => first.HasSameInputsAs(second))
                                                && HaveSameEntries(this.Colors, otherState.Colors, IsSameText)
                                                && HaveSameEntries(this.Dates, otherState.Dates, IsSameText)
                                                && HaveSameEntries(this.DateRanges, otherState.DateRanges, IsSameText)
                                                && HaveSameEntries(this.Times, otherState.Times, IsSameText);

    /// <summary>
    /// Copies all entries from one dictionary into another dictionary.
    /// </summary>
    /// <typeparam name="TKey">The dictionary key type.</typeparam>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="source">The source dictionary.</param>
    /// <param name="target">The target dictionary.</param>
    /// <param name="copyValue">Copies a value which the form changes in place; null for a value nobody can change, such as a string.</param>
    private static void CopyDictionary<TKey, TValue>(Dictionary<TKey, TValue> source, Dictionary<TKey, TValue> target, Func<TValue, TValue>? copyValue = null) where TKey : notnull
    {
        foreach (var (key, value) in source)
            target[key] = copyValue is null ? value : copyValue(value);
    }

    /// <summary>
    /// Tells whether two dictionaries hold the same keys with the same values.
    /// </summary>
    /// <typeparam name="TValue">The dictionary value type.</typeparam>
    /// <param name="first">The first dictionary.</param>
    /// <param name="second">The second dictionary.</param>
    /// <param name="isSame">Tells whether two values are the same.</param>
    /// <returns><c>true</c> when both dictionaries hold the same entries; otherwise, <c>false</c>.</returns>
    private static bool HaveSameEntries<TValue>(Dictionary<string, TValue> first, Dictionary<string, TValue> second, Func<TValue, TValue, bool> isSame) => first.Count == second.Count && first.All(entry => second.TryGetValue(entry.Key, out var otherValue) && isSame(entry.Value, otherValue));

    private static bool IsSameText(string first, string second) => string.Equals(first, second, StringComparison.Ordinal);

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

        if (this.FileAttachments.TryGetValue(fieldName, out var fileAttachmentState))
        {
            expectedType = "string[]";
            if (value.TryRead<LuaTable>(out var fileAttachmentTable))
            {
                fileAttachmentState.DocumentPaths = ReadFileAttachmentValues(fileAttachmentTable);
                return true;
            }

            if (!value.TryRead<string>(out var fileAttachmentValue))
                return false;

            fileAttachmentState.DocumentPaths = string.IsNullOrWhiteSpace(fileAttachmentValue) ? [] : [FileAttachment.FromPath(fileAttachmentValue)];
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
        if (this.FileAttachments.TryGetValue(name, out var fileAttachmentsValue))
            return AssistantLuaConversion.CreateLuaArray(
                fileAttachmentsValue.DocumentPaths
                    .OrderBy(static attachment => attachment.FilePath, StringComparer.Ordinal)
                    .Select(static attachment => attachment.FilePath));
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

    private static HashSet<FileAttachment> ReadFileAttachmentValues(LuaTable values)
    {
        var parsedValues = new HashSet<FileAttachment>();

        foreach (var entry in values)
        {
            if (entry.Value.TryRead<string>(out var value) && !string.IsNullOrWhiteSpace(value))
                parsedValues.Add(FileAttachment.FromPath(value));
        }

        return parsedValues;
    }
}
