using System.Net.Http.Headers;
using Azure.Core;

namespace ToolboxesAgent.Api.Handlers;

public sealed class BearerTokenHandler(
    TokenCredential credential,
    string scope) : DelegatingHandler(new HttpClientHandler())
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext([scope]),
            cancellationToken);

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.Token);

        return await base.SendAsync(request, cancellationToken);
    }
}
