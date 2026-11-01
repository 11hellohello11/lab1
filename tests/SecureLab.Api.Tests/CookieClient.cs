using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SecureLab.Api.Tests;

public static class CookieClient
{
    public static async Task<HttpClient> LoginAsync(
        this SecureLabApiFactory factory,
        string userName,
        string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { userName, password });

        if (!response.IsSuccessStatusCode)
        {
            client.Dispose();
            throw new InvalidOperationException(
                "Study login failed (credentials omitted).");
        }

        return client;
    }

    public static async Task<HttpResponseMessage> PostWithCsrfAsync<T>(
        this HttpClient client,
        string url,
        T data)
    {
        using var tokenResponse = await client.GetAsync(
            "/api/antiforgery/token");

        tokenResponse.EnsureSuccessStatusCode();

        var tokenData = await tokenResponse.Content
            .ReadFromJsonAsync<CsrfTokenResponse>();

        if (string.IsNullOrEmpty(tokenData?.Token))
        {
            throw new InvalidOperationException(
                "Antiforgery token was not returned.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(data)
        };

        request.Headers.Add("X-CSRF-TOKEN", tokenData.Token);

        return await client.SendAsync(request);
    }

    private sealed record CsrfTokenResponse(string Token);
}