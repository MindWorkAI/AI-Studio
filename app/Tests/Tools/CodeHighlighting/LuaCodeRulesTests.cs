using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class LuaCodeRulesTests
{
    [TestCase("local function greet(name)", "{keyword:local} {keyword:function} {function:greet}(name)")]
    [TestCase("function M.greet(name)", "{keyword:function} M.{function:greet}(name)", Description = "The function is greet, not the table it belongs to.")]
    [TestCase("-- note", "{comment:-- note}")]
    [TestCase("--[[ long\ncomment ]] x", "{comment:--[[ long\ncomment ]]} x")]
    [TestCase("local s = [==[ a ]] b ]==]", "{keyword:local} s = {string:[==[ a ]] b ]==]}")]
    [TestCase("if x == nil then return true end", "{keyword:if} x == {literal:nil} {keyword:then} {keyword:return} {literal:true} {keyword:end}")]
    [TestCase("t[1] = \"a\"", "t[{number:1}] = {string:\"a\"}")]
    [TestCase("obj:method()", "obj:{function:method}()")]
    public void LuaGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, LuaCodeRules.RULES), Is.EqualTo(expected));
    }
}