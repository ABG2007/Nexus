using Nexus.Core;
using Nexus.Core.Services;
using Xunit;

namespace Nexus.Core.Tests;

public class CommandInterpreterTests
{
    private readonly CommandInterpreter _sut = new();

    [Theory]
    [InlineData("github.com", CommandKind.Navigate)]
    [InlineData("https://clickup.com/docs", CommandKind.Navigate)]
    [InlineData("47*89", CommandKind.Calculate)]
    [InlineData("(3+4)*2", CommandKind.Calculate)]
    [InlineData("translate bonjour", CommandKind.Translate)]
    [InlineData("how does webview2 work?", CommandKind.AskAi)]
    [InlineData("what is oklch", CommandKind.AskAi)]
    [InlineData("best noise cancelling headphones", CommandKind.Search)]
    public void Classifies_intent(string input, CommandKind expected)
        => Assert.Equal(expected, _sut.Interpret(input).Kind);

    [Fact]
    public void Calculates_inline()
    {
        var intent = _sut.Interpret("12*12");
        Assert.Equal(CommandKind.Calculate, intent.Kind);
        Assert.Equal("144", intent.Result);
    }

    [Fact]
    public void Navigate_normalizes_scheme()
        => Assert.Equal("https://example.com", _sut.Interpret("example.com").Payload);

    [Fact]
    public void Empty_input_is_search()
        => Assert.Equal(CommandKind.Search, _sut.Interpret("   ").Kind);

    [Fact]
    public void Translate_strips_keyword()
        => Assert.Equal("hello world", _sut.Interpret("translate hello world").Payload);
}