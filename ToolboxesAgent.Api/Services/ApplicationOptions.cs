namespace ToolboxesAgent.Api.Services;

public class ApplicationOptions
{
    public string ProjectEndpoint { get; set; } = string.Empty;
    public string ModelDeploymentName { get; set; } = string.Empty;
    public string ToolboxName { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string AgentInstructions { get; set; } = string.Empty;
}
