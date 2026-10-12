namespace MiniAgent.Agent;

/// <summary>Human-in-the-loop gate for tools marked RequiresApproval.</summary>
public interface IApprovalHandler
{
    bool Approve(string toolName, string inputJson);
}

public sealed class ConsoleApprovalHandler : IApprovalHandler
{
    public bool Approve(string toolName, string inputJson)
    {
        Console.Write($"\n⚠️  Agent wants to run {toolName} {inputJson}\n   Allow? (y/n): ");
        return Console.ReadLine()?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true;
    }
}
