using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CLI.LMS.Api.Tests;

// The settings the hosted demo depends on: health check, Swagger flag,
// web client at the root, rate limit.
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
    public async Task Swagger_Enabled_ServesSwaggerUi()
    {
        using var factory = new LmsApiFactory(swaggerEnabled: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Root_ServesWebClient(bool swaggerEnabled)
    {
        using var factory = new LmsApiFactory(swaggerEnabled);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<title>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticFiles_DoNotCountTowardRateLimit()
    {
        using var factory = new LmsApiFactory();
        var client = factory.CreateClient();

        for (var i = 0; i < 70; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/app.js")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
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
