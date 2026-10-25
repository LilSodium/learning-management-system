using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CLI.LMS.Api.Tests;

// The settings the hosted demo depends on: health check, Swagger flag, rate limit.
public class HostingTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_IsOffByDefaultOutsideDevelopment()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_Enabled_RootRedirectsToSwagger()
    {
        using var factory = new LmsApiFactory(swaggerEnabled: true);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RateLimit_Returns429AfterSixtyRequestsInAMinute()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 60; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
