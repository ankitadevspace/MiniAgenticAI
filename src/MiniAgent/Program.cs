using MiniAgent.Agent;
using MiniAgent.Rag;
using MiniAgent.Tools;

var docsPath = "docs";
var notesPath = "notes.txt";
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--docs") docsPath = args[i + 1];
    else if (args[i] == "--notes") notesPath = args[i + 1];
}

LlmSettings settings;
try
{
    settings = LlmSettings.FromEnvironment();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var knowledge = new KnowledgeBase();
try
{
    knowledge.Add(DocumentLoader.LoadDirectory(docsPath));
}
catch (DirectoryNotFoundException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Run from the repository root, or pass --docs <folder>.");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) }; // local models can be slow
var llm = LlmFactory.Create(settings, http);

var tools = new ToolRegistry(new ITool[]
{
    new SearchDocsTool(knowledge),
    new CalculatorTool(),
    new CurrentTimeTool(),
    new SaveNoteTool(notesPath)
});

var agent = new AgentRunner(llm, tools, new ConsoleApprovalHandler(), new AgentOptions(), PrintTrace);

Console.WriteLine("MiniAgent - a tool-using AI agent with RAG");
Console.WriteLine($"Provider: {settings.Provider} | Model: {settings.Model} | Knowledge: {knowledge.ChunkCount} chunks from '{docsPath}'");
Console.WriteLine("Commands: /reset (clear memory), /help, /quit");

while (true)
{
    Console.Write("\nYou> ");
    var line = Console.ReadLine();
    if (line is null) break;

    line = line.Trim();
    if (line.Length == 0) continue;
    if (line is "/quit" or "/exit") break;

    if (line == "/reset")
    {
        agent.Reset();
        Console.WriteLine("Conversation memory cleared.");
        continue;
    }

    if (line == "/help")
    {
        Console.WriteLine("Ask about refunds, shipping, support hours or pricing, ask for math, or ask it to save a note.");
        continue;
    }

    try
    {
        var result = await agent.RunAsync(line);
        Console.WriteLine($"\nAgent> {result.FinalAnswer}");
        PrintTrace($"(steps: {result.Steps}, tokens in/out: {result.InputTokens}/{result.OutputTokens})");
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine($"\nLLM request failed: {ex.Message}");
        if (settings.Provider == "ollama")
            Console.Error.WriteLine($"Hint: is Ollama running, and did you run 'ollama pull {settings.Model}'?");
    }
    catch (TaskCanceledException)
    {
        Console.Error.WriteLine("\nThe request timed out. Try a smaller model or a shorter question.");
    }
}

return 0;

static void PrintTrace(string message)
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine(message);
    Console.ResetColor();
}
