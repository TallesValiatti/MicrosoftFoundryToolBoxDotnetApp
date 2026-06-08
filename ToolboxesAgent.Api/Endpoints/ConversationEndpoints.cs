using ToolboxesAgent.Api.Dtos;
using ToolboxesAgent.Api.Services;

namespace ToolboxesAgent.Api.Endpoints;

public static partial class ConversationEndpoints
{
    public static void MapConversationEndpoints(this WebApplication app)
    {
        app.MapPost("/conversations", CreateConversationAsync);
    }

    private static async Task<IResult> CreateConversationAsync(MsftFoundryService service)
    {
        var conversationId = await service.CreateConversationAsync();

        return Results.Ok(new CreateConversationResponse(conversationId));
    }
}
