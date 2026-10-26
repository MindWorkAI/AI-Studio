using AIStudio.Settings;

using Lua;
using Lua.Standard;

using SharedTools;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks how a format template is read from a configuration plugin, and that what the app exports
/// can be read back.
/// </summary>
/// <remarks>
/// The file is the one value of a format template which reaches beyond the plugin: a relative path
/// has to stay inside the plugin directory, and an absolute one must not be looked up, because an
/// import reads a pasted snippet the same way, before the user agreed to anything.
/// </remarks>
[TestFixture]
public sealed class FormatTemplateConfigurationTests
{
    private const string ID = "6b8e1a2c-3d4f-4a5b-9c6d-7e8f9a0b1c2d";

    private static readonly Guid PLUGIN_ID = new("33333333-3333-3333-3333-333333333333");

    private string pluginDirectory = string.Empty;

    [SetUp]
    public void CreatePluginDirectory()
    {
        this.pluginDirectory = Path.Combine(Path.GetTempPath(), $"format-template-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(this.pluginDirectory, "templates"));
        File.WriteAllBytes(Path.Combine(this.pluginDirectory, "templates", "Report.dotx"), [1]);
        File.WriteAllBytes(Path.Combine(this.pluginDirectory, "Report.pdf"), [1]);
    }

    [TearDown]
    public void DeletePluginDirectory() => Directory.Delete(this.pluginDirectory, true);

    [Test]
    public async Task WhatTheAppExportsIsWhatAConfigurationPluginCanReadBack()
    {
        var written = new FormatTemplate(3, Guid.NewGuid().ToString(), "Annual \"report\"", Path.Combine(Path.GetTempPath(), "templates", "Annual report.dotx"));
        var (isValid, read) = await ParseAsync(written.ExportAsConfigurationSection(), string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(read.Name, Is.EqualTo(written.Name));
            Assert.That(read.FilePath, Is.EqualTo(written.FilePath));
            Assert.That(read.IsEnterpriseConfiguration, Is.True);
            Assert.That(read.EnterpriseConfigurationPluginId, Is.EqualTo(PLUGIN_ID));
            Assert.That(read.Num, Is.Zero, "The number is given when the object is stored.");
        });
    }

    [Test]
    public async Task AFileOfThePluginIsFoundByItsRelativePath()
    {
        var (isValid, read) = await ParseAsync(Snippet(ID, "Report", "templates/Report.dotx"), this.pluginDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True);
            Assert.That(read.FilePath, Is.EqualTo(Path.Combine(Path.GetFullPath(this.pluginDirectory), "templates", "Report.dotx")));
        });
    }

    [Test]
    public async Task AnAbsolutePathIsNotLookedUp()
    {
        var absolutePath = Path.Combine(this.pluginDirectory, "nowhere", "Letter.dotx");
        var (isValid, read) = await ParseAsync(Snippet(ID, "Letter", absolutePath), this.pluginDirectory);

        Assert.Multiple(() =>
        {
            Assert.That(isValid, Is.True, "A network share which is not connected right now is no reason to drop the template.");
            Assert.That(read.FilePath, Is.EqualTo(absolutePath));
        });
    }

    [TestCase("../Report.dotx", Description = "A relative path must not leave the plugin directory.")]
    [TestCase("templates/Missing.dotx", Description = "A file the plugin ships has to exist.")]
    [TestCase("Report.pdf", Description = "A file which is no format template serves no export.")]
    public async Task AFileWhichCannotServeAsTemplateIsRejected(string filePath) => Assert.That((await ParseAsync(Snippet(ID, "Report", filePath), this.pluginDirectory)).IsValid, Is.False);

    [TestCase("no GUID", "Report", "templates/Report.dotx")]
    [TestCase(ID, "", "templates/Report.dotx")]
    [TestCase(ID, "Report", "")]
    public async Task ATemplateWithoutIdNameOrFileIsRejected(string id, string name, string filePath) => Assert.That((await ParseAsync(Snippet(id, name, filePath), this.pluginDirectory)).IsValid, Is.False);

    private static string Snippet(string id, string name, string filePath) =>
        $$"""CONFIG["FORMAT_TEMPLATES"][#CONFIG["FORMAT_TEMPLATES"]+1] = { ["Id"] = {{LuaTools.ToLuaStringLiteral(id)}}, ["Name"] = {{LuaTools.ToLuaStringLiteral(name)}}, ["FilePath"] = {{LuaTools.ToLuaStringLiteral(filePath)}} }""";

    /// <summary>
    /// Reads a format template the way a configuration plugin states it.
    /// </summary>
    /// <remarks>
    /// Through a real Lua state rather than a table put together in C#, so that the exported code
    /// has to be valid Lua before anything else is checked.
    /// </remarks>
    /// <param name="luaCode">The lines a plugin would contain, including the assignment itself.</param>
    /// <param name="pluginPath">The directory of the plugin, against which a relative path is resolved.</param>
    /// <returns>Whether the format template could be read, and the format template.</returns>
    private static async Task<(bool IsValid, FormatTemplate FormatTemplate)> ParseAsync(string luaCode, string pluginPath)
    {
        var state = LuaState.Create();
        state.OpenBasicLibrary();
        state.OpenTableLibrary();

        await state.DoStringAsync($$"""
                                    CONFIG = {}
                                    CONFIG["FORMAT_TEMPLATES"] = {}
                                    {{luaCode}}
                                    """);

        if (!state.Environment["CONFIG"].TryRead<LuaTable>(out var configTable) ||
            !configTable["FORMAT_TEMPLATES"].TryRead<LuaTable>(out var templatesTable) ||
            !templatesTable[1].TryRead<LuaTable>(out var templateTable))
            throw new InvalidOperationException("The code of this test did not produce a format template table.");

        var isValid = FormatTemplate.TryParseFormatTemplateTable(1, templateTable, PLUGIN_ID, pluginPath, out var parsed);
        return (isValid, (FormatTemplate)parsed);
    }
}