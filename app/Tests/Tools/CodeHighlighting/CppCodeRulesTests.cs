using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class CppCodeRulesTests
{
    [TestCase("#include <stdio.h>", "{meta:#include <stdio.h>}")]
    [TestCase("int main(void) {", "{type:int} {function:main}({type:void}) {")]
    [TestCase("std::vector<int> v{1, 2};", "std::vector<{type:int}> v{{number:1}, {number:2}};")]
    [TestCase("enum class Color { Red };", "{keyword:enum} {keyword:class} {type:Color} { Red };")]
    [TestCase("auto p = nullptr; const char* s = u8\"x\";", "{keyword:auto} p = {literal:nullptr}; {keyword:const} {type:char}* s = {string:u8\"x\"};")]
    [TestCase("unsigned long x = 0xFFUL; float f = 1.5f;", "{type:unsigned} {type:long} x = {number:0xFFUL}; {type:float} f = {number:1.5f};")]
    [TestCase("template <typename T> T max(T a, T b) { return a > b ? a : b; }", "{keyword:template} <{keyword:typename} T> T {function:max}(T a, T b) { {keyword:return} a > b ? a : b; }")]
    [TestCase("struct point { int x; };", "{keyword:struct} {type:point} { {type:int} x; };")]
    public void CAndCppGetTheirColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, CppCodeRules.RULES), Is.EqualTo(expected));
    }
}