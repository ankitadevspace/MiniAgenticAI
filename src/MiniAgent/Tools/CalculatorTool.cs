using System.Data;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MiniAgent.Tools;

public sealed class CalculatorTool : ITool
{
    // Whitelist: only digits and arithmetic characters ever reach the evaluator.
    private static readonly Regex Allowed = new(@"^[0-9+\-*/().%\s]+$", RegexOptions.Compiled);

    public string Name => "calculator";

    public string Description => "Evaluate an arithmetic expression such as '499 * 12' or '(100 - 20) / 4'.";

    public JsonObject InputSchema => JsonNode.Parse("""
        {
          "type": "object",
          "properties": { "expression": { "type": "string", "description": "Arithmetic expression" } },
          "required": ["expression"]
        }
        """)!.AsObject();

    public bool RequiresApproval => false;

    public Task<string> ExecuteAsync(JsonNode input, CancellationToken ct)
    {
        var expression = ToolInput.RequireString(input, "expression");

        if (!Allowed.IsMatch(expression))
            throw new ArgumentException("Expression may only contain digits, + - * / % ( ) and decimal points.");

        var result = Convert.ToString(new DataTable().Compute(expression, null), CultureInfo.InvariantCulture);
        return Task.FromResult(string.IsNullOrWhiteSpace(result) ? "Could not compute." : result);
    }
}
