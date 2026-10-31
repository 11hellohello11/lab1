
using System.Net;
using System.Text.Json;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class Lab04AuthorizationTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string StudyPassword =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException(
            "Set local SeedUsers__Password outside Git.");

    // A-02: Alice може читати власний інцидент.
    [Fact]
    public async Task A02_Owner_CanReadOwnIncident()
    {
        using var alice = await factory.LoginAsync("alice", StudyPassword);

        using var response = await alice.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            DbSeeder.AliceIncidentId,
            document.RootElement.GetProperty("id").GetGuid());
    }

    // A-03: Alice не може читати чужий інцидент Bob.
    [Fact]
    public async Task A03_NonOwner_CannotReadForeignIncident()
    {
        using var alice = await factory.LoginAsync("alice", StudyPassword);

        using var response = await alice.GetAsync(
            $"/api/incidents/{DbSeeder.BobIncidentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            404,
            document.RootElement.GetProperty("status").GetInt32());

        Assert.False(
            document.RootElement.TryGetProperty("ownerDisplayName", out _));
    }

    // A-03R: Analyst Morgan може читати інцидент Alice.
    [Fact]
    public async Task A03R_Analyst_CanReadIncidentForAnalysis()
    {
        using var morgan = await factory.LoginAsync("morgan", StudyPassword);

        using var response = await morgan.GetAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            DbSeeder.AliceIncidentId,
            document.RootElement.GetProperty("id").GetGuid());
    }
}
