# ToolboxesAgent.Api

A **.NET** minimal Web API that programmatically creates a **Microsoft Foundry** agent backed by a **Toolbox**, exposing it through a stateful conversation interface.

On the first request the API bootstraps all required Foundry resources: it creates a Toolbox version (with a **Microsoft Learn MCP Server** and **Web Search** tool), registers a project connection, and creates a declarative agent. Subsequent requests reuse those resources via lazy singleton initialization.

## What's inside

* `Program.cs`: registers services, configures DI, and maps the two API endpoints
* `Services/AgentService.cs`: orchestrates Toolbox creation, project connection setup, agent registration, and message routing using `AIProjectClient` and `ProjectResponsesClient`
* `Services/FoundryOptions.cs`: strongly typed options class bound to the `Foundry` section of `appsettings.json`
* `Endpoints/ConversationEndpoints.cs`: `POST /conversations` handler that creates a new Foundry conversation and returns its ID
* `Endpoints/MessageEndpoints.cs`: `POST /messages` handler that sends a user message to the agent and returns the generated response
* `Dtos/`: request and response records (`CreateConversationResponse`, `SendMessageRequest`, `SendMessageResponse`)
* `appsettings.json`: configuration template for all Foundry settings
* `ToolboxesAgent.Api.http`: sample HTTP requests for manual testing

## Prerequisites

* **.NET SDK** compatible with `net10.0`
* An active **Azure subscription**
* A **Microsoft Foundry** project with at least one model deployment (e.g. `gpt-4o`)
* **Azure CLI** installed and authenticated (`az login`) for local development

## Configure credentials

This app uses `DefaultAzureCredential`. No secrets are stored in environment variables. All Foundry settings are read from `appsettings.json` (or overridden via `appsettings.Development.json`).

Fill in the `Foundry` section before running:

```json
"Foundry": {
  "ProjectEndpoint": "<https://<account>.services.ai.azure.com/api/projects/<project>>",
  "ProjectResourceId": "</subscriptions/<id>/resourceGroups/<rg>/providers/Microsoft.CognitiveServices/accounts/<account>/projects/<project>>",
  "ModelDeploymentName": "gpt-4o",
  "ToolboxName": "<your-toolbox-name>",
  "ToolboxDescription": "<your-toolbox-description>",
  "ToolboxMcpServerLabel": "<your-toolbox-mcp-label>",
  "ToolboxMcpServerUrl": "https://learn.microsoft.com/api/mcp",
  "AgentName": "<your-agent-name>",
  "AgentInstructions": "<your-agent-instructions>",
  "AgentMcpServerLabel": "<your-agent-mcp-label>",
  "ProjectConnectionName": "<your-project-connection-name>",
  "ProjectConnectionAudience": "https://ai.azure.com"
}
```

Authenticate locally before running:

```bash
az login
```

## Run

```bash
dotnet run
```

The API starts on `http://localhost:5011` (or the port configured in `Properties/launchSettings.json`). On the first request, Foundry resource bootstrapping runs and logs output to the console:

```
Toolbox: <name> v<version>
Toolbox MCP endpoint: <url>
Project connection: <name>
Agent: <name> v<version>
```

To test with the included HTTP file, open `ToolboxesAgent.Api.http` in VS Code and send the requests in order: first `POST /conversations` to obtain a `conversationId`, then `POST /messages` with that ID and your message.

## Customizing

* **Change tools**: edit `CreateToolboxVersionAsync` in `AgentService.cs` to add or replace `ProjectsAgentTool` entries (e.g. swap the MCP server URL or add Azure AI Search).
* **Change agent behavior**: update `AgentInstructions` in `appsettings.json` to modify the agent's system prompt.
* **Change model**: set `ModelDeploymentName` to any model deployed in your Foundry project.
* **Pin NuGet versions**: replace `Version="*-*"` in `ToolboxesAgent.Api.csproj` with a specific preview version if you need a stable build.

## References

Microsoft Foundry Agent Service overview: https://learn.microsoft.com/en-us/azure/foundry/agents/overview  
Foundry Toolboxes: https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/toolbox  
Responses API: https://learn.microsoft.com/en-us/azure/foundry/openai/how-to/responses  
Azure.AI.Projects.OpenAI package: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.projects.openai-readme?view=azure-dotnet-preview  
Microsoft Learn MCP Server: https://learn.microsoft.com/en-us/training/support/mcp  
DefaultAzureCredential: https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/credential-chains#defaultazurecredential-overview  
