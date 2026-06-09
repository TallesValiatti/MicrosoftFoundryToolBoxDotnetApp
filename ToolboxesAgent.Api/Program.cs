using Azure.Core;
using Azure.Identity;
using ToolboxesAgent.Api.Endpoints;
using ToolboxesAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<ApplicationOptions>(
    builder.Configuration.GetSection("Application"));

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<AgentService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapConversationEndpoints();
app.MapMessageEndpoints();

app.Run();
