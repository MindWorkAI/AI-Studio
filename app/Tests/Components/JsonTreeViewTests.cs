using System.Text.Json.Nodes;

using AIStudio.Components;

namespace AIStudio.Tests.Components;

/// <summary>
/// Checks how the tree of a tool result in the chat writes its keys and values.
/// </summary>
/// <remarks>
/// The tree writes each key and each value as JSON. Escaping for a web page, as the default encoder of
/// System.Text.Json does, would show every umlaut and every quotation mark as a code of six characters,
/// although Blazor encodes the text for the page anyway. A value parsed from JSON goes through the same
/// writer as one created in code, so both get checked.
/// </remarks>
[TestFixture]
public sealed class JsonTreeViewTests
{
    [TestCase("Größe der Übersicht")]
    [TestCase("if (a < b && c > d) return 'x' + y;")]
    [TestCase("日本語のテキスト")]
    public void ValuesAppearAsWritten(string text)
    {
        var parsed = JsonNode.Parse(new JsonObject { ["text_content"] = text }.ToJsonString())!["text_content"];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonTreeView.FormatValue(JsonValue.Create(text)), Is.EqualTo($"\"{text}\""), "A value created in code.");
            Assert.That(JsonTreeView.FormatValue(parsed), Is.EqualTo($"\"{text}\""), "A value parsed from JSON.");
        }
    }

    [TestCase("Größe")]
    [TestCase("a < b && c > d")]
    public void KeysAppearAsWritten(string key) => Assert.That(JsonTreeView.FormatKey(key), Is.EqualTo($"\"{key}\""));

    [Test]
    public void WhatJsonHasToEscapeStaysEscaped()
    {
        const string TEXT = "A \"quoted\" line\nand a backslash \\ at its end.";
        using (Assert.EnterMultipleScope())
        {
            Assert.That(JsonNode.Parse(JsonTreeView.FormatValue(JsonValue.Create(TEXT)))?.GetValue<string>(), Is.EqualTo(TEXT), "A value.");
            Assert.That(JsonNode.Parse(JsonTreeView.FormatKey(TEXT))?.GetValue<string>(), Is.EqualTo(TEXT), "A key.");
        }
    }
}