using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Banking.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Banking.Tests;

public sealed class ApiFixture : IAsyncLifetime
{
    private WebApplication app = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => app.Services;
    public RegisteredCustomer Alice { get; private set; } = null!;
    public RegisteredCustomer Bob { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        app = Program.CreateApplication(new[]
        {
            "--urls", "http://127.0.0.1:0", "--environment", "Development",
            "--https_port", "7260",
            "--Logging:LogLevel:Default", "Critical"
        }, builder =>
        {
            builder.Logging.ClearProviders();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(addresses.Addresses.Single())
        };
        Alice = await RegisterAsync("Alice");
        Bob = await RegisterAsync("Bob");
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    public async Task<RegisteredCustomer> RegisterAsync(string name)
    {
        var email = $"{name.ToLowerInvariant()}.{Guid.NewGuid():N}@example.com";
        using var response = await PostAsync("/api/customer/register",
            new { customer = new { firstName = name, lastName = "Test", email } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var customer = body.GetProperty("customer");
        Assert.Equal(4, customer.EnumerateObject().Count());
        Assert.Matches("^[0-9]{6}$", body.GetProperty("pin").GetString()!);
        return new RegisteredCustomer(customer.GetProperty("customerNumber").GetString()!, email,
            body.GetProperty("pin").GetString()!, body.GetProperty("sessionToken").GetString()!);
    }

    public async Task<string> CreateAccountAsync(string token, object? request = null)
    {
        using var response = await PostAsync("/api/account/create", request ?? new { }, token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("account").GetProperty("accountNumber").GetString()!;
    }

    public Task<HttpResponseMessage> PostAsync(string path, object? body, string? token = null) =>
        SendAsync(HttpMethod.Post, path, body is null ? null : JsonContent.Create(body), token);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path,
        HttpContent? content = null, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = content };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(request);
        Assert.True(response.Headers.CacheControl?.NoStore);
        if ((int)response.StatusCode >= 400)
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return response;
    }

    public sealed record RegisteredCustomer(string Number, string Email, string Pin, string Token)
    {
        public override string ToString() => "RegisteredCustomer(credentials=redacted)";
    }
}
