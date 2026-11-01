using System.Net;

namespace SecureLab.Api.Tests;

public sealed class SearchMechanicsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task Search_ReturnsJson()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/incidents/search?q=навчальн");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Search_SqlInjectionInput_IsTreatedAsData()
    {
        using var client = factory.CreateClient();

        const string payload = "' OR '1'='1";
        var url = "/api/incidents/search?q=" +
            Uri.EscapeDataString(payload);

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal("[]", json.Trim());
    }
}
