using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace ToolboxesAgent.Api.Services;

public class MsftFoundryService(
    IOptions<MsftFoundryOptions> options,
    TokenCredential credential)
{
    private readonly MsftFoundryOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task<string> CreateConversationAsync()
    {
        AIProjectClient projectClient = CreateProjectClient();

        await EnsureToolboxAndAgentAsync(projectClient);

        ProjectConversation conversation = await projectClient
            .ProjectOpenAIClient
            .GetProjectConversationsClient()
            .CreateProjectConversationAsync();

        return conversation.Id;
    }

    public async Task<string> SendMessageAsync(string conversationId, string message)
    {
        AIProjectClient projectClient = CreateProjectClient();

        await EnsureToolboxAndAgentAsync(projectClient);

        ProjectResponsesClient responsesClient = projectClient
            .ProjectOpenAIClient
            .GetProjectResponsesClientForAgent(
                defaultAgent: _options.AgentName,
                defaultConversationId: conversationId);

        ResponseResult response = await responsesClient.CreateResponseAsync(message);

        return response.GetOutputText();
    }

    private AIProjectClient CreateProjectClient() =>
        new(
            endpoint: new Uri(_options.ProjectEndpoint),
            tokenProvider: credential);

    private async Task EnsureToolboxAndAgentAsync(AIProjectClient projectClient)
    {
        if (_initialized)
            return;

        await _initializationLock.WaitAsync();

        try
        {
            if (_initialized)
                return;

            CreateToolboxVersion(projectClient);

            CreateAgentVersion(projectClient);

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private void CreateToolboxVersion(AIProjectClient projectClient)
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

        toolboxClient.CreateToolboxVersion(
            name: _options.ToolboxName,
            tools:
            [
                microsoftLearnMcpTool,
                webSearchTool,
                toolboxSearchTool
            ],
            description: "Toolbox with Microsoft Learn MCP, Web Search and Tool Search.");
    }

    private void CreateAgentVersion(AIProjectClient projectClient)
    {
        string toolboxConsumerEndpoint = GetToolboxConsumerEndpoint();

        McpTool toolboxMcpTool = ResponseTool.CreateMcpTool(
            serverLabel: "foundry-toolbox",
            serverUri: new Uri(toolboxConsumerEndpoint),
            toolCallApprovalPolicy: new McpToolCallApprovalPolicy(
                GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));

        DeclarativeAgentDefinition agentDefinition = new(_options.ModelDeploymentName)
        {
            Instructions = _options.AgentInstructions,
            Tools = { toolboxMcpTool }
        };

        projectClient.AgentAdministrationClient.CreateAgentVersion(
            agentName: _options.AgentName,
            options: new(agentDefinition));
    }

    private string GetToolboxConsumerEndpoint() =>
        $"{_options.ProjectEndpoint.TrimEnd('/')}/toolboxes/{_options.ToolboxName}/mcp?api-version=v1";
}
