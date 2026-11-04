using System.Text.Json;

namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// Recognizes the language of a code block which names none behind its opening fence.
/// </summary>
/// <remarks>
/// Users and models often leave the language out. Before AI Studio highlighted code itself, highlight.js
/// guessed it in the browser; now the app guesses, for the highlighting and for the file the block is
/// saved as, which therefore always agree. It guesses only among the languages it highlights, and only
/// when one of them clearly wins: a file saved under a wrong extension is worse than a text file. JSON
/// and markup are recognized by their shape. Every other language has markers, things which hint at
/// it, such as using System; for C#; each marker counts once, so that a long block does not win by
/// repeating one.
/// </remarks>
public static class CodeLanguageDetector
{
    /// <summary>
    /// The weight the best language needs at least.
    /// </summary>
    private const int MINIMUM_WEIGHT = 4;

    /// <summary>
    /// How far the best language has to be ahead of the next one.
    /// </summary>
    private const int MINIMUM_LEAD = 2;

    /// <summary>
    /// The weight the markers of an extended language, such as TypeScript, need to win over the language it extends.
    /// </summary>
    private const int MINIMUM_REFINEMENT_WEIGHT = 2;

    private static readonly IReadOnlyList<CodeLanguageProfile> PROFILES =
    [
        new("python",
        [
            First("#!/usr/bin/env python", 4), First("#!/usr/bin/python", 4), Start("def ", 2), End(":", 1), Start("elif ", 3),
            Anywhere(" import ", 2), Anywhere("self.", 2), Anywhere("__init__", 3), Anywhere("__name__", 3), Anywhere("print(", 1),
            Anywhere("None", 1),
        ]),

        new("bash",
        [
            First("#!/bin/bash", 4), First("#!/bin/sh", 4), First("#!/usr/bin/env bash", 4), First("#!/bin/zsh", 4),
            Start("sudo ", 4), Start("npm ", 4), Start("npx ", 4), Start("pip install ", 4), Start("apt ", 4), Start("apt-get ", 4),
            Start("brew ", 4), Start("git ", 4), Start("docker ", 4), Start("curl ", 4), Start("chmod ", 4), Start("cargo ", 4),
            Start("dotnet ", 4), Start("cd ", 3), Start("mkdir ", 3), Start("echo ", 2), Start("export ", 1), Line("fi", 3),
            Line("done", 3), Line("esac", 3), Anywhere("; then", 3), Anywhere("; do", 3), Anywhere(" | grep ", 3), Anywhere("${", 1),
        ]),

        new("powershell",
        [
            Anywhere("Write-Host", 4), Anywhere("Write-Output", 4), Anywhere("-Object", 3), Anywhere("Get-", 2), Anywhere("Set-", 2),
            Anywhere("New-", 2), Anywhere("$_", 3), Anywhere("$env:", 4), Anywhere("-ErrorAction", 4), Anywhere(" -eq ", 3),
            Anywhere(" -ne ", 3), Anywhere(" -like ", 3), Start("param(", 3), Anywhere("[string]", 2),
        ]),

        new("lua",
        [
            Start("local function ", 4), Start("local ", 2), Line("end", 2), End(" then", 2), Anywhere(" ~= ", 3), Anywhere("ipairs(", 4),
            Anywhere("pairs(", 3), Anywhere(" .. ", 1), Anywhere("nil", 1),
        ]),

        new("javascript",
        [
            Start("const ", 2), Start("let ", 2), Start("var ", 1), Start("function ", 2), Start("import ", 1), Start("export default ", 3),
            Anywhere("=> ", 1), Anywhere("console.log(", 4), Anywhere("document.", 3), Anywhere("window.", 3), Anywhere("require(", 2),
            Anywhere("module.exports", 4), Anywhere(" from '", 3), Anywhere(" from \"", 3), Anywhere("===", 3), Anywhere("!==", 3),
            Anywhere(".then(", 2), Anywhere("addEventListener(", 3), Anywhere("JSON.stringify(", 3), Anywhere("undefined", 2),
        ], "typescript",
        [
            Anywhere(": string", 2), Anywhere(": number", 2), Anywhere(": boolean", 2), Start("interface ", 2), Start("export interface ", 2),
            Start("type ", 1), Start("import type ", 2), Anywhere(" as const", 2), Anywhere("): void", 2), Anywhere("<T>", 1),
        ]),

        new("c",
        [
            Start("#include <", 4), Start("#include \"", 4), Start("#define ", 3), Anywhere("printf(", 2), Anywhere("int main(", 3),
            Anywhere("malloc(", 3), Anywhere("sizeof(", 2), Anywhere("->", 1),
        ], "cpp",
        [
            Anywhere("std::", 3), Anywhere("cout <<", 3), Anywhere("cin >>", 3), Start("template <", 3), Start("template<", 3),
            Anywhere("nullptr", 2), Start("namespace ", 1), Start("class ", 1),
        ]),

        new("csharp",
        [
            Start("using System", 4), Start("namespace ", 2), Anywhere("{ get; set; }", 4), Anywhere("{ get; }", 3),
            Anywhere("Console.WriteLine(", 4), Anywhere("static void Main(", 4), Anywhere("async Task", 3), Anywhere("nameof(", 4),
            Anywhere("<summary>", 4), Anywhere("Dictionary<", 2), Anywhere(".Select(", 2), Anywhere("new()", 2), Start("public class ", 2),
            Start("var ", 1), Anywhere("=> ", 1),
        ]),

        new("java",
        [
            Start("import java.", 4), Start("import javax.", 4), Anywhere("System.out.println(", 4), Anywhere("static void main(String[] args)", 4),
            Start("package ", 2), Start("@Override", 3), Anywhere(" implements ", 2), Anywhere(" throws ", 2), Anywhere("ArrayList<", 2),
            Start("public class ", 2), Anywhere(" extends ", 1),
        ]),

        new("kotlin",
        [
            Anywhere("fun main(", 4), Start("data class ", 4), Start("import kotlin", 4), Anywhere("mutableListOf(", 4),
            Anywhere("override fun ", 4), Start("fun ", 3), Start("val ", 2), Anywhere("listOf(", 3), Anywhere("?.let", 3),
            Anywhere("println(", 2),
        ]),

        new("go",
        [
            Start("package main", 4), Anywhere("fmt.Println(", 4), Anywhere("fmt.Printf(", 4), Anywhere("err != nil", 4),
            Anywhere("go func(", 4), Start("func ", 3), Anywhere(" := ", 3), Start("import (", 3), Anywhere("chan ", 2),
        ]),

        new("rust",
        [
            Start("pub fn ", 4), Start("use std::", 4), Anywhere("let mut ", 4), Anywhere("println!(", 4), Start("#[derive(", 4),
            Start("fn ", 3), Start("impl ", 3), Anywhere("&str", 3), Anywhere("-> Result<", 3), Anywhere("unwrap()", 3),
            Anywhere("Vec<", 2), Anywhere("Option<", 2),
        ]),

        new("sql",
        [
            Start("INSERT INTO ", 4, true), Start("CREATE TABLE ", 4, true), Start("DELETE FROM ", 4, true), Start("ALTER TABLE ", 4, true),
            Start("DROP TABLE ", 4, true), Start("SELECT ", 3, true), Anywhere("GROUP BY ", 3, true), Anywhere("ORDER BY ", 3, true),
            Anywhere("INNER JOIN ", 3, true), Start("UPDATE ", 2, true), Start("WHERE ", 2, true), Anywhere(" SET ", 1, true),
            Anywhere(" FROM ", 1, true),
        ]),

        new("css",
        [
            Start("@media ", 4), Start(":root", 4), Start("body {", 3), Anywhere("!important", 3), Anywhere("font-size: ", 3),
            Anywhere("px;", 3), Anywhere("rem;", 3), Anywhere("em;", 2), Anywhere("color: ", 2), Anywhere("margin: ", 2),
            Anywhere("padding: ", 2), Anywhere("display: ", 2), Anywhere("background: ", 2), Anywhere("border: ", 2),
        ]),

        new("yaml",
        [
            Start("apiVersion: ", 4), Start("runs-on: ", 4), Line("services:", 4), Start("kind: ", 3), Line("steps:", 3),
            Line("jobs:", 3), Start("uses: ", 3), Start("- uses: ", 3), Start("image: ", 2), Line("env:", 1),
        ]),
    ];

    /// <summary>
    /// The languages the detector recognizes, as a model would write them behind the fence.
    /// </summary>
    internal static IEnumerable<string> FenceLanguages => ["json", "html", "xml", ..PROFILES.Select(profile => profile.FenceLanguage), ..PROFILES.Where(profile => profile.Refinement is not null).Select(profile => profile.Refinement!)];

    /// <summary>
    /// The language of a code block: the one behind its fence or, when it names none, the one its code
    /// is written in.
    /// </summary>
    /// <param name="fenceLanguage">The language behind the opening fence, as Markdig reads it.</param>
    /// <param name="code">The code of the block.</param>
    /// <returns>The language behind the fence, when there is one, even when it is text. Otherwise, the
    /// language of the code, or null when it is not clear.</returns>
    public static string? FenceLanguageOf(string? fenceLanguage, string code) => string.IsNullOrWhiteSpace(fenceLanguage) ? Detect(code) : fenceLanguage;

    /// <summary>
    /// Recognizes the language a code is written in.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The language as a model would write it behind the fence, for example csharp, or null when
    /// no language clearly wins.</returns>
    public static string? Detect(string code)
    {
        var text = code.AsSpan().Trim();
        if (text.IsEmpty)
            return null;

        if (IsJson(text))
            return "json";

        if (text[0] is '<' && (text.Contains("</", StringComparison.Ordinal) || text.Contains("/>", StringComparison.Ordinal)))
            return text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || text.Contains("xmlns", StringComparison.Ordinal) ? "xml" : "html";

        string? bestLanguage = null;
        var bestWeight = 0;
        var secondWeight = 0;
        foreach (var profile in PROFILES)
        {
            var refinementWeight = profile.RefinementMarkers is { } refinementMarkers ? Weigh(text, refinementMarkers) : 0;
            var weight = Weigh(text, profile.Markers) + refinementWeight;
            if (weight > bestWeight)
            {
                secondWeight = bestWeight;
                bestWeight = weight;
                bestLanguage = profile.Refinement is not null && refinementWeight >= MINIMUM_REFINEMENT_WEIGHT ? profile.Refinement : profile.FenceLanguage;
            }
            else if (weight > secondWeight)
                secondWeight = weight;
        }

        return bestWeight >= MINIMUM_WEIGHT && bestWeight - secondWeight >= MINIMUM_LEAD ? bestLanguage : null;
    }

    private static bool IsJson(ReadOnlySpan<char> text)
    {
        if (text[0] is not ('{' or '[') || text[^1] is not ('}' or ']'))
            return false;

        try
        {
            using var document = JsonDocument.Parse(text.ToString());
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int Weigh(ReadOnlySpan<char> text, IReadOnlyList<CodeLanguageMarker> markers)
    {
        var weight = 0;
        foreach (var marker in markers)
            if (Occurs(text, marker))
                weight += marker.Weight;

        return weight;
    }

    private static bool Occurs(ReadOnlySpan<char> text, CodeLanguageMarker marker)
    {
        var comparison = marker.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (marker.Place is CodeMarkerPlace.ANYWHERE)
            return text.Contains(marker.Text, comparison);

        foreach (var line in text.EnumerateLines())
        {
            var trimmed = line.Trim();
            if (trimmed.IsEmpty)
                continue;

            var occurs = marker.Place switch
            {
                CodeMarkerPlace.LINE_START or CodeMarkerPlace.FIRST_LINE => trimmed.StartsWith(marker.Text, comparison),
                CodeMarkerPlace.LINE_END => trimmed.EndsWith(marker.Text, comparison),
                CodeMarkerPlace.WHOLE_LINE => trimmed.Equals(marker.Text, comparison),

                _ => false,
            };

            if (occurs)
                return true;

            //
            // A shebang counts only on the first line:
            //
            if (marker.Place is CodeMarkerPlace.FIRST_LINE)
                return false;
        }

        return false;
    }

    private static CodeLanguageMarker Anywhere(string text, int weight, bool ignoreCase = false) => new(text, CodeMarkerPlace.ANYWHERE, weight, ignoreCase);

    private static CodeLanguageMarker Start(string text, int weight, bool ignoreCase = false) => new(text, CodeMarkerPlace.LINE_START, weight, ignoreCase);

    private static CodeLanguageMarker End(string text, int weight) => new(text, CodeMarkerPlace.LINE_END, weight);

    private static CodeLanguageMarker Line(string text, int weight) => new(text, CodeMarkerPlace.WHOLE_LINE, weight);

    private static CodeLanguageMarker First(string text, int weight) => new(text, CodeMarkerPlace.FIRST_LINE, weight);
}