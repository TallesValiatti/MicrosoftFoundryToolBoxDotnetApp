using ToolboxesAgent.Api.Dtos;
using ToolboxesAgent.Api.Services;

namespace ToolboxesAgent.Api.Endpoints;

public static partial class MessageEndpoints
{
    public static void MapMessageEndpoints(this WebApplication app)
    {
        app.MapPost("/messages", SendMessageAsync);
    }

    private static async Task<IResult> SendMessageAsync(
        SendMessageRequest request,
        AgentService service)
    {
        var response = await service.SendMessageAsync(request.ConversationId, request.Message);

        return Results.Ok(new SendMessageResponse(response));
    }
}
