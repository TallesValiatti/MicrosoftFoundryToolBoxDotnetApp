using System.Net.Http.Headers;
using System.Net.Http.Json;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Microsoft.Extensions.Options;
using OpenAI.Responses;

namespace ToolboxesAgent.Api.Services;

public class AgentService(
    IOptions<FoundryOptions> options,
    TokenCredential credential,
    IHttpClientFactory httpClientFactory)
{
    private const string ManagementScope = "https://management.azure.com/.default";

    private readonly FoundryOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private ProjectsAgentVersion? _agentVersion;

    public async Task<string> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        await EnsureFoundryResourcesAsync(cancellationToken);

        AIProjectClient projectClient = CreateProjectClient();

        Azure.AI.Extensions.OpenAI.ProjectConversation conversation = await projectClient.ProjectOpenAIClient
            .GetProjectConversationsClient()
            .CreateProjectConversationAsync(
                new Azure.AI.Extensions.OpenAI.ProjectConversationCreationOptions(),
                cancellationToken);

        return conversation.Id;
    }

    public async Task<string> SendMessageAsync(
        string conversationId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        await EnsureFoundryResourcesAsync(cancellationToken);

        AIProjectClient projectClient = CreateProjectClient();

        Azure.AI.Extensions.OpenAI.AgentReference agentReference = new(name: _options.AgentName, version: null);

        Azure.AI.Extensions.OpenAI.ProjectResponsesClient responsesClient = projectClient.ProjectOpenAIClient
            .GetProjectResponsesClientForAgent(
                defaultAgent: agentReference,
                defaultConversationId: conversationId);

        ResponseResult response = await responsesClient.CreateResponseAsync(
            userInputText: userMessage,
            previousResponseId: null,
            cancellationToken: cancellationToken);

        return response.GetOutputText();
    }

    private async Task EnsureFoundryResourcesAsync(CancellationToken cancellationToken)
    {
        if (_agentVersion is not null)
            return;

        await _initializationLock.WaitAsync(cancellationToken);

        try
        {
            if (_agentVersion is not null)
                return;

            AIProjectClient projectClient = CreateProjectClient();

            ToolboxVersion toolboxVersion = await CreateToolboxVersionAsync(projectClient, cancellationToken);

            string toolboxMcpEndpoint = BuildToolboxMcpEndpoint();

            await CreateProjectConnectionAsync(toolboxMcpEndpoint, cancellationToken);

            _agentVersion = CreateAgentVersion(projectClient, toolboxMcpEndpoint);

            Console.WriteLine($"Toolbox: {toolboxVersion.Name} v{toolboxVersion.Version}");
            Console.WriteLine($"Toolbox MCP endpoint: {toolboxMcpEndpoint}");
            Console.WriteLine($"Project connection: {_options.ProjectConnectionName}");
            Console.WriteLine($"Agent: {_agentVersion.Name} v{_agentVersion.Version}");
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private AIProjectClient CreateProjectClient() =>
        new(
            endpoint: new Uri(_options.ProjectEndpoint),
            tokenProvider: credential);

    private async Task<ToolboxVersion> CreateToolboxVersionAsync(
        AIProjectClient projectClient,
        CancellationToken cancellationToken)
    {
        AgentToolboxes toolboxClient =
            projectClient.AgentAdministrationClient.GetAgentToolboxes();

        McpTool mcpTool = ResponseTool.CreateMcpTool(
            serverLabel: _options.ToolboxMcpServerLabel,
            serverUri: new Uri(_options.ToolboxMcpServerUrl),
            toolCallApprovalPolicy: new McpToolCallApprovalPolicy(
                GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));

        ProjectsAgentTool mcpProjectTool = ProjectsAgentTool.AsProjectTool(mcpTool);

        ProjectsAgentTool webSearchProjectTool = ProjectsAgentTool.AsProjectTool(
            ResponseTool.CreateWebSearchTool());

        return await toolboxClient.CreateToolboxVersionAsync(
            name: _options.ToolboxName,
            tools: [mcpProjectTool, webSearchProjectTool],
            description: _options.ToolboxDescription,
            cancellationToken: cancellationToken);
    }

    private async Task CreateProjectConnectionAsync(
        string toolboxMcpEndpoint,
        CancellationToken cancellationToken)
    {
        AccessToken managementToken = await credential.GetTokenAsync(
            new TokenRequestContext([ManagementScope]),
            cancellationToken);

        string requestUri =
            $"https://management.azure.com{_options.ProjectResourceId.TrimEnd('/')}/connections/{_options.ProjectConnectionName}?api-version=2025-10-01-preview";

        using HttpClient httpClient = httpClientFactory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, requestUri);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", managementToken.Token);

        request.Content = JsonContent.Create(new
        {
            name = _options.ProjectConnectionName,
            type = "Microsoft.MachineLearningServices/workspaces/connections",
            properties = new
            {
                authType = "ProjectManagedIdentity",
                category = "RemoteTool",
                target = toolboxMcpEndpoint,
                isSharedToAll = true,
                audience = _options.ProjectConnectionAudience,
                metadata = new
                {
                    ApiType = "Azure"
                }
            }
        });

        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Failed to create Foundry project connection. Status: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }
    }

    private ProjectsAgentVersion CreateAgentVersion(
        AIProjectClient projectClient,
        string toolboxMcpEndpoint)
    {
        McpTool toolboxMcpTool = ResponseTool.CreateMcpTool(
            serverLabel: _options.AgentMcpServerLabel,
            serverUri: new Uri(toolboxMcpEndpoint),
            toolCallApprovalPolicy: new McpToolCallApprovalPolicy(
                GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));

        toolboxMcpTool.ProjectConnectionId = _options.ProjectConnectionName;

        DeclarativeAgentDefinition agentDefinition = new(_options.ModelDeploymentName)
        {
            Instructions = _options.AgentInstructions,
            Tools =
            {
                toolboxMcpTool
            }
        };

        return projectClient.AgentAdministrationClient.CreateAgentVersion(
            agentName: _options.AgentName,
            options: new(agentDefinition));
    }

    private string BuildToolboxMcpEndpoint() =>
        $"{_options.ProjectEndpoint.TrimEnd('/')}/toolboxes/{_options.ToolboxName}/mcp?api-version=v1";
}
