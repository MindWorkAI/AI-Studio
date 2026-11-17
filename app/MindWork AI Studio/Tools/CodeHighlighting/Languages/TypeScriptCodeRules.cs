namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How TypeScript code looks to the tokenizer, TSX included: JavaScript with types.
/// </summary>
/// <remarks>
/// TypeScript names its own types in PascalCase, such as Promise or User, so a name in PascalCase is a
/// type here, unlike in JavaScript.
/// </remarks>
public static class TypeScriptCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(
        [
            ..JavaScriptCodeRules.KEYWORDS,
            "abstract", "as", "declare", "enum", "implements", "infer", "interface", "is", "keyof", "namespace", "override",
            "private", "protected", "public", "readonly", "satisfies", "type",
        ]),

        Types = CodeLanguageRules.Words("any", "bigint", "boolean", "never", "number", "object", "string", "symbol", "unknown"),
        Literals = CodeLanguageRules.Words(JavaScriptCodeRules.LITERALS),
        FunctionDeclarations = CodeLanguageRules.Words("function"),
        TypeDeclarations = CodeLanguageRules.Words("class", "enum", "interface", "type"),
        LineComments = ["//"],
        BlockComments = [new("/*", "*/")],
        Strings = JavaScriptCodeRules.STRINGS,
        AnnotationPrefix = '@',
        WordStartCharacters = "$",
        WordCharacters = "$",
        HighlightPascalCaseTypes = true,
    };
}