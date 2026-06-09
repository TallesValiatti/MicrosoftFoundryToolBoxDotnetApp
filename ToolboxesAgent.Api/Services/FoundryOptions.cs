namespace ToolboxesAgent.Api.Services;

public class FoundryOptions
{
    public string ProjectEndpoint { get; set; } = string.Empty;
    public string ProjectResourceId { get; set; } = string.Empty;
    public string ModelDeploymentName { get; set; } = string.Empty;

    public string ToolboxName { get; set; } = string.Empty;
    public string ToolboxDescription { get; set; } = string.Empty;
    public string ToolboxMcpServerLabel { get; set; } = string.Empty;
    public string ToolboxMcpServerUrl { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;
    public string AgentInstructions { get; set; } = string.Empty;
    public string AgentMcpServerLabel { get; set; } = string.Empty;

    public string ProjectConnectionName { get; set; } = string.Empty;
    public string ProjectConnectionAudience { get; set; } = string.Empty;
}
