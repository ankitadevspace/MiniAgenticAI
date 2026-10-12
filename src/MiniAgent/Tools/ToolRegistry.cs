using MiniAgent.Agent;

namespace MiniAgent.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
    }

    public ITool? Find(string name) => _tools.GetValueOrDefault(name);

    public IReadOnlyList<ToolSpec> Specs() =>
        _tools.Values.Select(t => new ToolSpec(t.Name, t.Description, t.InputSchema)).ToList();
}
