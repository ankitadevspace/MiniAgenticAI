using System.Text.Json.Nodes;
using MiniAgent.Tools;

namespace MiniAgent.Tests;

public class CalculatorToolTests
{
    [Theory]
    [InlineData("499 * 12", "5988")]
    [InlineData("(2 + 3) * 4", "20")]
    [InlineData("100 - 1", "99")]
    public async Task Evaluates_arithmetic(string expression, string expected)
    {
        var result = await new CalculatorTool().ExecuteAsync(new JsonObject { ["expression"] = expression }, default);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("System.IO.File.Delete('x')")]
    [InlineData("1 + abc")]
    public async Task Rejects_anything_that_is_not_arithmetic(string expression)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CalculatorTool().ExecuteAsync(new JsonObject { ["expression"] = expression }, default));
    }

    [Fact]
    public async Task Missing_argument_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CalculatorTool().ExecuteAsync(new JsonObject(), default));
    }
}
