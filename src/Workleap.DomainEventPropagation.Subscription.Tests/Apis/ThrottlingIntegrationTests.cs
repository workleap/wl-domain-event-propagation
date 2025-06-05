using System.Net;
using System.Net.Http.Json;
using Azure.Messaging.EventGrid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Workleap.DomainEventPropagation.Subscription.Tests.Apis;

[Collection(XunitCollectionConstants.StaticActivitySensitive)]
public class ThrottlingIntegrationTests(ThrottlingIntegrationTestsFixture fixture) : IClassFixture<ThrottlingIntegrationTestsFixture>
{
    private readonly HttpClient _httpClient = fixture.CreateClient();

    [Fact]
    public async Task GivenMultipleConcurrentRequests_WhenThrottlingEnabled_ThenSomeRequestsAreThrottled()
    {
        // Arrange
        var wrapperEvent = DomainEventWrapper.Wrap(new DummyDomainEvent { PropertyB = 1, PropertyA = "Hello world" });
        var eventGridEvent = new EventGridEvent(
            subject: "subject",
            eventType: "dummy",
            dataVersion: "1.0",
            data: new BinaryData(wrapperEvent.Data))
        {
            Topic = ThrottlingIntegrationTestsFixture.TestTopic,
        };

        // Act
        var requestCount = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        for (var i = 0; i < requestCount; i++)
        {
            tasks.Add(this._httpClient.PostAsJsonAsync("/eventgrid/domainevents", new[] { eventGridEvent }));
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        Assert.Contains(responses, r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GivenMultipleConcurrentRequestsUnderLimit_WhenThrottlingEnabled_ThenNoThrottling()
    {
        // Arrange
        var wrapperEvent = DomainEventWrapper.Wrap(new DummyDomainEvent { PropertyB = 1, PropertyA = "Hello world" });
        var eventGridEvent = new EventGridEvent(
            subject: "subject",
            eventType: "dummy",
            dataVersion: "1.0",
            data: new BinaryData(wrapperEvent.Data))
        {
            Topic = ThrottlingIntegrationTestsFixture.TestTopic,
        };

        // Act
        var tasks = new List<Task<HttpResponseMessage>>();

        for (var i = 0; i < ThrottlingIntegrationTestsFixture.MaxRequests; i++)
        {
            tasks.Add(this._httpClient.PostAsJsonAsync("/eventgrid/domainevents", new[] { eventGridEvent }));
        }

        // Wait for all requests to complete
        var responses = await Task.WhenAll(tasks);

        Assert.DoesNotContain(responses, r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests);
    }
}

public sealed class ThrottlingIntegrationTestsFixture : WebApplicationFactory<ThrottlingIntegrationTestsFixture.Startup>
{
    public const string TestTopic = "DummyTopic";
    public const int MaxRequests = 5;
    public const int QueueLimit = 1;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services => { });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [$"{EventPropagationThrottlingOptions.DefaultSectionName}:MaxEventsPerSecond"] = MaxRequests.ToString(),
                [$"{EventPropagationThrottlingOptions.DefaultSectionName}:QueueLimit"] = QueueLimit.ToString(),
            }!)
            .Build();

        builder.UseConfiguration(configuration);
        base.ConfigureWebHost(builder);
    }

    protected override IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
    }

    public class Startup(IConfiguration configuration)
    {
        public IConfiguration Configuration { get; } = configuration;

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddEventPropagationSubscriber()
                .AddDomainEventHandler<DummyDomainEvent, DummyDomainEventHandler>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapEventPropagationEndpoint();
            });
        }
    }
}