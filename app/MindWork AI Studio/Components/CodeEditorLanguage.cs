namespace AIStudio.Components;

/// <summary>
/// Selects the syntax highlighter used by <see cref="CodeEditor"/>.
/// The enum value is passed to the JavaScript module as a string, so a new
/// language must also be handled in <c>wwwroot/system/CodeEditor/code-editor.js</c>.
/// </summary>
public enum CodeEditorLanguage
{
    PLAIN_TEXT,
    LUA,
}
