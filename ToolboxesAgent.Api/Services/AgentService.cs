using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OpenAI.Responses;
using ToolboxesAgent.Api.Handlers;

namespace ToolboxesAgent.Api.Services;

public class AgentService(
    IOptions<ApplicationOptions> options,
    TokenCredential credential)
{
    private const string FoundryScope = "https://ai.azure.com/.default";
    private const string ToolboxPreviewHeaderName = "Foundry-Features";
    private const string ToolboxPreviewHeaderValue = "Toolboxes=V1Preview";

    private readonly ApplicationOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly Dictionary<string, AgentSession> _sessions = new();
    private bool _toolboxInitialized;

    public async Task<string> CreateConversationAsync()
    {
        AIProjectClient projectClient = CreateProjectClient();

        await EnsureToolboxAsync(projectClient);

        string toolboxEndpoint = GetToolboxConsumerEndpoint();
        AIAgent agent = await CreateAgentAsync(projectClient, toolboxEndpoint);

        AgentSession session = await agent.CreateSessionAsync();

        string conversationId = Guid.NewGuid().ToString("N");

        lock (_sessions)
        {
            _sessions[conversationId] = session;
        }

        return conversationId;
    }

    public async Task<string> SendMessageAsync(string conversationId, string message)
    {
        AIProjectClient projectClient = CreateProjectClient();

        await EnsureToolboxAsync(projectClient);

        string toolboxEndpoint = GetToolboxConsumerEndpoint();
        AIAgent agent = await CreateAgentAsync(projectClient, toolboxEndpoint);

        AgentSession session;

        lock (_sessions)
        {
            if (!_sessions.TryGetValue(conversationId, out session!))
            {
                throw new InvalidOperationException(
                    $"Conversation '{conversationId}' was not found. Create a conversation before sending messages.");
            }
        }

        AgentResponse response = await agent.RunAsync(
            message: message,
            session: session);

        return response.Text;
    }

    private AIProjectClient CreateProjectClient() =>
        new(
            endpoint: new Uri(_options.ProjectEndpoint),
            tokenProvider: credential);

    private async Task EnsureToolboxAsync(AIProjectClient projectClient)
    {
        if (_toolboxInitialized)
            return;

        await _initializationLock.WaitAsync();

        try
        {
            if (_toolboxInitialized)
                return;

            await CreateToolboxVersionAsync(projectClient);

            _toolboxInitialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task CreateToolboxVersionAsync(AIProjectClient projectClient)
    {
        AgentToolboxes toolboxClient =
            projectClient.AgentAdministrationClient.GetAgentToolboxes();

        ProjectsAgentTool microsoftLearnMcpTool = ProjectsAgentTool.AsProjectTool(
            ResponseTool.CreateMcpTool(
                serverLabel: "microsoft-learn",
                serverUri: new Uri("https://learn.microsoft.com/api/mcp"),
                toolCallApprovalPolicy: new McpToolCallApprovalPolicy(
                    GlobalMcpToolCallApprovalPolicy.NeverRequireApproval)));

        ProjectsAgentTool webSearchTool = ProjectsAgentTool.AsProjectTool(
            ResponseTool.CreateWebSearchTool());

        ToolboxSearchPreviewTool toolboxSearchTool = new()
        {
            Name = "ToolBoxSearch",
            Description = "Search for tools by capability."
        };

        ToolboxVersion toolboxVersion = await toolboxClient.CreateToolboxVersionAsync(
            name: _options.ToolboxName,
            tools:
            [
                microsoftLearnMcpTool,
                webSearchTool,
                toolboxSearchTool
            ],
            description: "Toolbox with Microsoft Learn MCP, Web Search and Tool Search.");

        Console.WriteLine($"Toolbox created: {toolboxVersion.Name}");
        Console.WriteLine($"Toolbox version: {toolboxVersion.Version}");
        Console.WriteLine($"Toolbox MCP endpoint: {GetToolboxConsumerEndpoint()}");
    }

    private async Task<AIAgent> CreateAgentAsync(
        AIProjectClient projectClient,
        string toolboxEndpoint)
    {
        IList<AITool> toolboxTools = await LoadToolboxToolsAsync(toolboxEndpoint);

        return projectClient.AsAIAgent(
            model: _options.ModelDeploymentName,
            name: _options.AgentName,
            instructions: _options.AgentInstructions,
            tools: toolboxTools);
    }

    private async Task<IList<AITool>> LoadToolboxToolsAsync(string toolboxEndpoint)
    {
        using var httpClient = new HttpClient(
            new BearerTokenHandler(
                credential,
                FoundryScope));

        await using McpClient mcpClient = await McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(toolboxEndpoint),
                    Name = "foundry-toolbox",
                    TransportMode = HttpTransportMode.StreamableHttp,
                    AdditionalHeaders = new Dictionary<string, string>
                    {
                        [ToolboxPreviewHeaderName] = ToolboxPreviewHeaderValue
                    }
                },
                httpClient));

        IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();

        Console.WriteLine("Tools loaded from Foundry Toolbox:");

        foreach (McpClientTool tool in mcpTools)
        {
            Console.WriteLine($"- {tool.Name}");
        }

        return [.. mcpTools.Cast<AITool>()];
    }

    private string GetToolboxConsumerEndpoint() =>
        $"{_options.ProjectEndpoint.TrimEnd('/')}/toolboxes/{_options.ToolboxName}/mcp?api-version=v1";
}
