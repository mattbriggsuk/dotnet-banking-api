using System.Net;
using Banking.Api;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Banking.Tests;

public sealed class TransportTests
{
    [Fact]
    public async Task ProductionStillRedirectsHttpRequestsToHttps()
    {
        await using var app = Program.CreateApplication(new[]
        {
            "--urls", "http://127.0.0.1:0", "--environment", "Production", "--https_port", "7260"
        }, builder =>
        {
            builder.Logging.ClearProviders();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(addresses.Addresses.Single())
        };
        using var response = await client.GetAsync("/api/customer/get");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https", response.Headers.Location!.Scheme);
        Assert.Equal(7260, response.Headers.Location.Port);
        Assert.Equal("/api/customer/get", response.Headers.Location.AbsolutePath);
        await app.StopAsync();
    }
}
