namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How C and C++ code looks to the tokenizer. C++ knows all the words of C, so both share one set of
/// rules.
/// </summary>
/// <remarks>
/// A directive of the preprocessor, such as #include, runs to the end of its line. A raw string of C++,
/// such as R"(…)", reads like any other string, which differs only when it holds a quotation mark.
/// </remarks>
public static class CppCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(
            "alignas", "alignof", "asm", "auto", "break", "case", "catch", "class", "co_await", "co_return", "co_yield",
            "concept", "const", "const_cast", "consteval", "constexpr", "constinit", "continue", "decltype", "default",
            "delete", "do", "dynamic_cast", "else", "enum", "explicit", "export", "extern", "for", "friend", "goto", "if",
            "inline", "mutable", "namespace", "new", "noexcept", "operator", "private", "protected", "public", "register",
            "reinterpret_cast", "requires", "return", "sizeof", "static", "static_assert", "static_cast", "struct",
            "switch", "template", "this", "thread_local", "throw", "try", "typedef", "typeid", "typename", "union", "using",
            "virtual", "volatile", "while"),

        Types = CodeLanguageRules.Words(
            "bool", "char", "char8_t", "char16_t", "char32_t", "double", "float", "int", "int8_t", "int16_t", "int32_t",
            "int64_t", "intptr_t", "long", "ptrdiff_t", "short", "signed", "size_t", "uint8_t", "uint16_t", "uint32_t",
            "uint64_t", "uintptr_t", "unsigned", "void", "wchar_t"),

        Literals = CodeLanguageRules.Words("true", "false", "nullptr", "NULL"),
        TypeDeclarations = CodeLanguageRules.Words("class", "enum", "struct", "union"),
        LineComments = ["//"],
        BlockComments = [new("/*", "*/")],
        Strings =
        [
            new("\"", "\""),
            new("'", "'"),
        ],

        StringPrefixes = CodeLanguageRules.Words("L", "u", "U", "u8", "R", "LR", "uR", "UR", "u8R"),
        HasPreprocessor = true,
    };
}