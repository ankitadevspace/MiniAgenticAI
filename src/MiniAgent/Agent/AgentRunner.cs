using System.Text.Json.Nodes;
using MiniAgent.Tools;

namespace MiniAgent.Agent;

public sealed record AgentResult(
    string FinalAnswer,
    int Steps,
    bool HitStepLimit,
    int InputTokens,
    int OutputTokens);

/// <summary>
/// The agent loop: ask the LLM, run any tools it requests, feed the results back, and repeat
/// until the LLM answers without requesting a tool (or the step limit is hit).
/// History is kept between calls, so follow-up questions work (short-term memory).
/// </summary>
public sealed class AgentRunner
{
    private readonly ILlmClient _llm;
    private readonly ToolRegistry _tools;
    private readonly IApprovalHandler _approval;
    private readonly AgentOptions _options;
    private readonly Action<string>? _log;
    private readonly List<ChatMessage> _history = new();

    public AgentRunner(
        ILlmClient llm,
        ToolRegistry tools,
        IApprovalHandler approval,
        AgentOptions options,
        Action<string>? log = null)
    {
        _llm = llm;
        _tools = tools;
        _approval = approval;
        _options = options;
        _log = log;
    }

    public void Reset() => _history.Clear();

    public async Task<AgentResult> RunAsync(string goal, CancellationToken ct = default)
    {
        var checkpoint = _history.Count;
        try
        {
            return await RunLoopAsync(goal, ct);
        }
        catch
        {
            // Roll back a half-finished turn so the history stays valid for the next goal.
            _history.RemoveRange(checkpoint, _history.Count - checkpoint);
            throw;
        }
    }

    private async Task<AgentResult> RunLoopAsync(string goal, CancellationToken ct)
    {
        _history.Add(new UserMessage(goal));

        var specs = _tools.Specs();
        int inputTokens = 0, outputTokens = 0;

        for (var step = 1; step <= _options.MaxSteps; step++)
        {
            var reply = await _llm.SendAsync(_options.SystemPrompt, specs, _history, ct);
            inputTokens += reply.InputTokens;
            outputTokens += reply.OutputTokens;

            var text = reply.Text.Trim();

            // No tool requested -> the agent considers the goal done.
            if (reply.ToolCalls.Count == 0)
            {
                if (text.Length == 0) text = "(The model returned an empty response.)";
                _history.Add(new AssistantMessage(text, Array.Empty<ToolCall>()));
                return new AgentResult(text, step, false, inputTokens, outputTokens);
            }

            _history.Add(new AssistantMessage(text, reply.ToolCalls));
            if (text.Length > 0)
                _log?.Invoke($"[step {step}] 💭 {text}");

            foreach (var call in reply.ToolCalls)
            {
                _log?.Invoke($"[step {step}] 🔧 {call.Name} {call.Input.ToJsonString()}");
                var (output, isError) = await ExecuteToolAsync(call, ct);
                _log?.Invoke($"[step {step}] 👀 {Truncate(output, 300)}");
                _history.Add(new ToolResultMessage(call.Id, call.Name, output, isError));
            }
        }

        // Keep the conversation valid so the user can continue afterwards.
        var message = $"I stopped because I reached the step limit ({_options.MaxSteps}) before finishing.";
        _history.Add(new AssistantMessage(message, Array.Empty<ToolCall>()));
        return new AgentResult(message, _options.MaxSteps, true, inputTokens, outputTokens);
    }

    private async Task<(string Output, bool IsError)> ExecuteToolAsync(ToolCall call, CancellationToken ct)
    {
        var tool = _tools.Find(call.Name);
        if (tool is null)
            return ($"Unknown tool '{call.Name}'.", true);

        if (tool.RequiresApproval && !_approval.Approve(call.Name, call.Input.ToJsonString()))
            return ("The user denied this action. Do not retry it; tell the user it was not performed.", false);

        try
        {
            return (await tool.ExecuteAsync(call.Input, ct), false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Let the model see the error so it can adapt instead of crashing the whole run.
            return ($"Tool error: {ex.Message}", true);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
