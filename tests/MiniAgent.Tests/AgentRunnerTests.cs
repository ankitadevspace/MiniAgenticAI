using System.Text.Json.Nodes;
using MiniAgent.Agent;
using MiniAgent.Tools;

namespace MiniAgent.Tests;

public class AgentRunnerTests
{
    private static AgentRunner Build(ScriptedLlm llm, bool approve, int maxSteps, params ITool[] tools) =>
        new(llm, new ToolRegistry(tools), new FakeApproval(approve), new AgentOptions { MaxSteps = maxSteps });

    private static JsonObject Calc(string expression) => new() { ["expression"] = expression };

    [Fact]
    public async Task Runs_a_tool_then_returns_the_final_answer()
    {
        var llm = new ScriptedLlm(Replies.CallTool("calculator", Calc("499 * 12")), Replies.Final("5988 rupees per year."));
        var agent = Build(llm, approve: true, maxSteps: 5, new CalculatorTool());

        var result = await agent.RunAsync("yearly cost?");

        Assert.Equal("5988 rupees per year.", result.FinalAnswer);
        Assert.Equal(2, result.Steps);
        Assert.False(result.HitStepLimit);
        Assert.Equal(30, result.InputTokens);
        Assert.Equal(13, result.OutputTokens);

        var toolResult = Assert.Single(llm.LastHistory.OfType<ToolResultMessage>());
        Assert.Equal("5988", toolResult.Content);
        Assert.False(toolResult.IsError);
    }

    [Fact]
    public async Task Denied_risky_tool_is_not_executed()
    {
        var spy = new SpyTool("save_note", requiresApproval: true);
        var llm = new ScriptedLlm(Replies.CallTool("save_note", new JsonObject()), Replies.Final("Okay, not saved."));
        var agent = Build(llm, approve: false, maxSteps: 5, spy);

        await agent.RunAsync("save this");

        Assert.False(spy.Executed);
        Assert.Contains(llm.LastHistory.OfType<ToolResultMessage>(), m => m.Content.Contains("denied"));
    }

    [Fact]
    public async Task Approved_risky_tool_is_executed()
    {
        var spy = new SpyTool("save_note", requiresApproval: true);
        var llm = new ScriptedLlm(Replies.CallTool("save_note", new JsonObject()), Replies.Final("Saved."));
        var agent = Build(llm, approve: true, maxSteps: 5, spy);

        await agent.RunAsync("save this");

        Assert.True(spy.Executed);
    }

    [Fact]
    public async Task Safe_tool_runs_without_asking_for_approval()
    {
        var spy = new SpyTool("lookup", requiresApproval: false);
        var llm = new ScriptedLlm(Replies.CallTool("lookup", new JsonObject()), Replies.Final("Found."));
        var agent = Build(llm, approve: false, maxSteps: 5, spy); // approval would say no, but isn't asked

        await agent.RunAsync("look it up");

        Assert.True(spy.Executed);
    }

    [Fact]
    public async Task Stops_at_the_step_limit()
    {
        var llm = new ScriptedLlm(Replies.CallTool("calculator", Calc("1 + 1"))); // never finishes
        var agent = Build(llm, approve: true, maxSteps: 3, new CalculatorTool());

        var result = await agent.RunAsync("loop forever");

        Assert.True(result.HitStepLimit);
        Assert.Equal(3, result.Steps);
        Assert.Equal(3, llm.Calls);
    }

    [Fact]
    public async Task Tool_failure_is_reported_to_the_model_instead_of_crashing()
    {
        var llm = new ScriptedLlm(Replies.CallTool("explode", new JsonObject()), Replies.Final("Sorry, that failed."));
        var agent = Build(llm, approve: true, maxSteps: 5, new ThrowingTool());

        var result = await agent.RunAsync("do it");

        Assert.Equal("Sorry, that failed.", result.FinalAnswer);
        Assert.Contains(llm.LastHistory.OfType<ToolResultMessage>(), m => m.IsError && m.Content.Contains("boom"));
    }

    [Fact]
    public async Task Unknown_tool_is_reported_as_an_error()
    {
        var llm = new ScriptedLlm(Replies.CallTool("does_not_exist", new JsonObject()), Replies.Final("I could not do that."));
        var agent = Build(llm, approve: true, maxSteps: 5, new CalculatorTool());

        await agent.RunAsync("do it");

        Assert.Contains(llm.LastHistory.OfType<ToolResultMessage>(), m => m.IsError && m.Content.Contains("Unknown tool"));
    }

    [Fact]
    public async Task History_is_kept_between_goals_and_cleared_by_reset()
    {
        var llm = new ScriptedLlm(Replies.Final("Hi!"));
        var agent = Build(llm, approve: true, maxSteps: 5);

        await agent.RunAsync("first");
        await agent.RunAsync("second");
        Assert.Equal(3, llm.LastHistory.Count); // user, assistant, user

        agent.Reset();
        await agent.RunAsync("third");
        Assert.Single(llm.LastHistory);
    }
}
