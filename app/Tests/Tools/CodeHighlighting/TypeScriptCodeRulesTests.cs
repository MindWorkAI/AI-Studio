using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class TypeScriptCodeRulesTests
{
    [TestCase("interface User { name: string; age?: number }", "{keyword:interface} {type:User} { name: {type:string}; age?: {type:number} }")]
    [TestCase("type Id = string | number;", "{keyword:type} {type:Id} = {type:string} | {type:number};")]
    [TestCase("function find(id: Id): Promise<User> {", "{keyword:function} {function:find}(id: {type:Id}): {type:Promise}<{type:User}> {")]
    [TestCase("private readonly cache = new Map<string, User>();", "{keyword:private} {keyword:readonly} cache = {keyword:new} {type:Map}<{type:string}, {type:User}>();")]
    [TestCase("const ok: boolean = true;", "{keyword:const} ok: {type:boolean} = {literal:true};")]
    public void TypeScriptGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, TypeScriptCodeRules.RULES), Is.EqualTo(expected));
    }
}