using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tests.Tools.CodeHighlighting;

[TestFixture]
public sealed class JavaScriptCodeRulesTests
{
    [TestCase("import { useState } from 'react';", "{keyword:import} { useState } {keyword:from} {string:'react'};")]
    [TestCase("const add = (a, b) => a + b;", "{keyword:const} add = (a, b) => a + b;")]
    [TestCase("function greet(name) { return `Hi ${name}`; }", "{keyword:function} {function:greet}(name) { {keyword:return} {string:`Hi ${name}`}; }")]
    [TestCase("class Dog extends Animal {}", "{keyword:class} {type:Dog} {keyword:extends} Animal {}")]
    [TestCase("if (x === null || y === undefined) console.log(NaN);", "{keyword:if} (x === {literal:null} || y === {literal:undefined}) console.{function:log}({literal:NaN});")]
    [TestCase("let $el = $('#id');", "{keyword:let} $el = {function:$}({string:'#id'});")]
    [TestCase("const n = 0xFF + 1e3 + 10n;", "{keyword:const} n = {number:0xFF} + {number:1e3} + {number:10n};")]
    [TestCase("// note /* not a block */", "{comment:// note /* not a block */}")]
    [TestCase("@Component class A {}", "{meta:@Component} {keyword:class} {type:A} {}")]
    public void JavaScriptGetsItsColors(string code, string expected)
    {
        Assert.That(CodeMarkup.Of(code, JavaScriptCodeRules.RULES), Is.EqualTo(expected));
    }
}