using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public sealed class Lab03SecurityTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static string StudyPassword =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException(
            "Set local SeedUsers__Password outside Git.");

    // A-01: анонімний користувач не має доступу до профілю
    [Fact]
    public async Task A01_AnonymousUser_CannotAccessMe()
    {
        using var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A-02: автентифікований користувач отримує власний профіль
    [Fact]
    public async Task A02_AuthenticatedUser_CanAccessMe()
    {
        using var alice = await factory.LoginAsync("alice", StudyPassword);

        using var response = await alice.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var userId = document.RootElement
            .GetProperty("userId")
            .GetString();

        var userName = document.RootElement
            .GetProperty("userName")
            .GetString();

        Assert.Equal(
            "10000000-0000-0000-0000-000000000001",
            userId);

        Assert.Equal("alice", userName);
    }
    // A-05: користувач не може підмінити власника інциденту
    [Fact]
    public async Task A05_ClientCannotForgeIncidentOwner()
    {
        using var alice = await factory.LoginAsync("alice", StudyPassword);

        var title = $"A05 owner protection {Guid.NewGuid()}";

        var request = new
        {
            title,
            description = "Testing that the server ignores a forged incident owner.",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),

            // навмисно вказуємо Bob замість Alice
            ownerUserId = DbSeeder.BobId
        };

        using var response = await alice.PostAsJsonAsync(
            "/api/incidents",
            request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var incidentId = document.RootElement.GetProperty("id").GetGuid();

        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SecureLabDbContext>();

        var actualOwnerId = await db.Incidents
            .Where(incident => incident.Id == incidentId)
            .Select(incident => incident.OwnerUserId)
            .SingleAsync();

        Assert.Equal(DbSeeder.AliceId, actualOwnerId);
        Assert.NotEqual(DbSeeder.BobId, actualOwnerId);
    }
}