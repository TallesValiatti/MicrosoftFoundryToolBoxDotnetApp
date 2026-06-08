using Azure.Core;
using Azure.Identity;
using ToolboxesAgent.Api.Endpoints;
using ToolboxesAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<MsftFoundryOptions>(
    builder.Configuration.GetSection("MsftFoundry"));

builder.Services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
builder.Services.AddSingleton<MsftFoundryService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapConversationEndpoints();
app.MapMessageEndpoints();

app.Run();
