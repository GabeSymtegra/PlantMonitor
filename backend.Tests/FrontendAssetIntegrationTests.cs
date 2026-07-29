using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace backend.Tests;

public sealed class FrontendAssetIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly Regex AssetReferenceRegex = new(
        @"(?:src|href)=""(?<url>[^""\n]+\.(?:js|css))(?:\?[^""\n]*)?""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly TestWebApplicationFactory _factory;

    public FrontendAssetIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RootHtml_ReferencesReachableJsAndCssAssets()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        var assetUrls = AssetReferenceRegex.Matches(html)
            .Select(match => match.Groups["url"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.NotEmpty(assetUrls);

        foreach (var assetUrl in assetUrls)
        {
            var assetResponse = await client.GetAsync(assetUrl);
            Assert.Equal(HttpStatusCode.OK, assetResponse.StatusCode);

            var mediaType = assetResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            if (assetUrl.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Contains("css", mediaType);
            }
            else
            {
                Assert.True(
                    mediaType.Contains("javascript", StringComparison.OrdinalIgnoreCase)
                    || mediaType.Contains("ecmascript", StringComparison.OrdinalIgnoreCase),
                    $"Unexpected content type '{mediaType}' for {assetUrl}");
            }
        }
    }
}
