using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class SqlCodeRulesTests
{
    [TestCase("SELECT name, COUNT(*) FROM users WHERE age >= 18 GROUP BY name;", "{keyword:SELECT} name, {function:COUNT}(*) {keyword:FROM} users {keyword:WHERE} age >= {number:18} {keyword:GROUP} {keyword:BY} name;")]
    [TestCase("select * from t where x is not null", "{keyword:select} * {keyword:from} t {keyword:where} x {keyword:is} {keyword:not} {literal:null}", Description = "SQL ignores the case of its keywords.")]
    [TestCase("INSERT INTO t (\"order\", `group`) VALUES ('it''s', 1.5);", "{keyword:INSERT} {keyword:INTO} t (\"order\", `group`) {keyword:VALUES} ({string:'it''s'}, {number:1.5});", Description = "A quoted name gets no color, even when it is a keyword.")]
    [TestCase("CREATE TABLE [user] (id INT PRIMARY KEY, name VARCHAR(100));", "{keyword:CREATE} {keyword:TABLE} [user] (id {type:INT} {keyword:PRIMARY} {keyword:KEY}, name {type:VARCHAR}({number:100}));")]
    [TestCase("-- note\n/* block */", "{comment:-- note}\n{comment:/* block */}")]
    public void SqlGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, SqlCodeRules.RULES), Is.EqualTo(expected));
    }
}