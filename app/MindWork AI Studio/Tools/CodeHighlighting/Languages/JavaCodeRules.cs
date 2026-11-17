namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How Java code looks to the tokenizer.
/// </summary>
/// <remarks>
/// Java names its classes in PascalCase, such as String or ArrayList, so such a name is a type, and a
/// constant in capitals, such as MAX_SIZE, is none. A text block in three quotation marks may run over
/// several lines.
/// </remarks>
public static class JavaCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(
            "abstract", "assert", "break", "case", "catch", "class", "const", "continue", "default", "do", "else", "enum",
            "extends", "final", "finally", "for", "goto", "if", "implements", "import", "instanceof", "interface", "native",
            "new", "package", "permits", "private", "protected", "public", "record", "return", "sealed", "static",
            "strictfp", "super", "switch", "synchronized", "this", "throw", "throws", "transient", "try", "var",
            "volatile", "while", "yield"),

        Types = CodeLanguageRules.Words("boolean", "byte", "char", "double", "float", "int", "long", "short", "void"),
        Literals = CodeLanguageRules.Words("true", "false", "null"),
        TypeDeclarations = CodeLanguageRules.Words("class", "enum", "interface", "record"),
        LineComments = ["//"],
        BlockComments = [new("/*", "*/")],
        Strings =
        [
            new("\"\"\"", "\"\"\"", IsMultiline: true),
            new("\"", "\""),
            new("'", "'"),
        ],

        AnnotationPrefix = '@',
        HighlightPascalCaseTypes = true,
    };
}