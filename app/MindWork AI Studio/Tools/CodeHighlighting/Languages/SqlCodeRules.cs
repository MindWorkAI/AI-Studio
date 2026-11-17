namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How SQL looks to the tokenizer, across its dialects, such as PostgreSQL, MySQL, or T-SQL.
/// </summary>
/// <remarks>
/// SQL ignores the case of its keywords. A string stands in apostrophes, two of which stand for one. A
/// name in quotation marks, backticks, or square brackets, such as "order", stays plain, so that no
/// keyword within it gets a color.
/// </remarks>
public static class SqlCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.WordsInAnyCase(
            "add", "all", "alter", "and", "as", "asc", "begin", "between", "by", "case", "cast", "check", "column", "commit",
            "constraint", "create", "cross", "database", "default", "delete", "desc", "distinct", "drop", "else", "end",
            "exists", "foreign", "from", "full", "group", "having", "if", "in", "index", "inner", "insert", "into", "is",
            "join", "key", "left", "like", "limit", "not", "offset", "on", "or", "order", "outer", "over", "partition",
            "primary", "references", "replace", "returning", "right", "rollback", "select", "set", "table", "then", "top",
            "transaction", "trigger", "truncate", "union", "unique", "update", "using", "values", "view", "when", "where",
            "with"),

        Types = CodeLanguageRules.WordsInAnyCase(
            "bigint", "binary", "bit", "blob", "boolean", "char", "date", "datetime", "decimal", "double", "float", "int",
            "integer", "interval", "json", "jsonb", "money", "nchar", "numeric", "nvarchar", "real", "serial", "smallint",
            "text", "time", "timestamp", "tinyint", "uuid", "varbinary", "varchar"),

        Literals = CodeLanguageRules.WordsInAnyCase("null", "true", "false"),
        LineComments = ["--"],
        BlockComments = [new("/*", "*/")],
        Strings = [new("'", "'", Escape: null, IsMultiline: true, DoubledCloseIsLiteral: true)],
        QuotedNames =
        [
            new("\"", "\"", Escape: null, DoubledCloseIsLiteral: true),
            new("`", "`", Escape: null),
            new("[", "]", Escape: null),
        ],
    };
}