using System.Threading.Channels;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Workleap.DomainEventPropagation.Tests;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public class PushDeliveryTests(ITestOutputHelper testOutputHelper)
{
    private const int EmulatorPort = 6500;
    private const string TopicName = "Topic1";
    private const string SubscriberEndpoint = "/my-webhook";
    private const string LocalUrl = "http://host.testcontainers.internal:5000";
    private const int EventGridId = 1;
    private const int CloudEventId = 2;

    [Fact]
    public async Task TestPublishAndReceiveEvent()
    {
        // Add a timeout for the test to not block indefinitely
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Enable activity tracking
        using var activityTracker = new InMemoryActivityTracker();

        // Start Event Grid Emulator
        await using var eventGridEmulator = await EventGridEmulatorContext.StartAsync(testOutputHelper);

        // Configure the publisher and the subscriptions, and start the service
        var host = this.BuildHost(eventGridEmulator.Url);
        var runTask = host.RunAsync(cts.Token); // The method returns when the services are running

        // Send an EventGrid event
        var client = host.Services.GetRequiredService<IEventPropagationClient>();
        await client.PublishDomainEventsAsync([new TestEventGridEvent { Id = EventGridId }], cts.Token);
        activityTracker.AssertPublishSuccessful("EventGridEvents create com.workleap.sample.testEventGridEvent");

        // Send a CloudEvent
        await client.PublishDomainEventsAsync([new TestCloudEvent { Id = CloudEventId }], cts.Token);
        activityTracker.AssertPublishSuccessful("CloudEvents create com.workleap.sample.testCloudEvent");

        // Read the events. The background service should call TestDomainEventHandler, so the channel should have 1 item each
        var eventGridChannel = host.Services.GetRequiredService<Channel<TestEventGridEvent>>();
        var processedEvent = await eventGridChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal(EventGridId, processedEvent.Id);
        activityTracker.AssertSubscribeSuccessful("EventGridEvents process com.workleap.sample.testEventGridEvent");
        activityTracker.AssertEventgridEventsTagsSet("EventGridEvents process com.workleap.sample.testEventGridEvent");

        var cloudChannel = host.Services.GetRequiredService<Channel<TestCloudEvent>>();
        var processedCloudEvent = await cloudChannel.Reader.ReadAsync(cts.Token);
        Assert.Equal(CloudEventId, processedCloudEvent.Id);
        activityTracker.AssertSubscribeSuccessful("CloudEvents process com.workleap.sample.testCloudEvent");
        activityTracker.AssertCloudEventsTagsSet("CloudEvents process com.workleap.sample.testCloudEvent");

        // Terminate the service
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
        await runTask;
    }

    private IHost BuildHost(string eventGridUrl)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Channel.CreateUnbounded<TestEventGridEvent>());
        builder.Services.AddSingleton(Channel.CreateUnbounded<TestCloudEvent>());
        builder.Services.AddEventPropagationPublisher(options =>
        {
            options.TopicEndpoint = $"{eventGridUrl}{TopicName}/api/events";
            options.TopicName = TopicName;
            options.TopicAccessKey = "noop";
            options.TopicType = TopicType.Custom;
        });
        builder.Services.AddEventPropagationSubscriber()
            .AddDomainEventHandler<TestEventGridEvent, TestDomainEventHandler>()
            .AddDomainEventHandler<TestCloudEvent, TestDomainEventHandler>();

        var webApp = builder.Build();

        webApp.MapEventPropagationEndpoint(SubscriberEndpoint);

        return webApp;
    }

    [DomainEvent("com.workleap.sample.testEventGridEvent")]
    private sealed record TestEventGridEvent : IDomainEvent
    {
        public int Id { get; set; }
    }

    [DomainEvent("com.workleap.sample.testCloudEvent", EventSchema.CloudEvent)]
    private sealed record TestCloudEvent : IDomainEvent
    {
        public int Id { get; set; }
    }

    private sealed class TestDomainEventHandler(Channel<TestEventGridEvent> eventGridChannel, Channel<TestCloudEvent> cloudChannel) : IDomainEventHandler<TestEventGridEvent>, IDomainEventHandler<TestCloudEvent>
    {
        public async Task HandleDomainEventAsync(TestEventGridEvent domainEvent, CancellationToken cancellationToken)
        {
            await eventGridChannel.Writer.WriteAsync(domainEvent, cancellationToken);
        }

        public async Task HandleDomainEventAsync(TestCloudEvent domainEvent, CancellationToken cancellationToken)
        {
            await cloudChannel.Writer.WriteAsync(domainEvent, cancellationToken);
        }
    }

    private sealed class EventGridEmulatorContext(IContainer container) : IAsyncDisposable
    {
        public string Url { get; } = $"http://localhost:{container.GetMappedPublicPort(EmulatorPort)}/";

        public static async Task<EventGridEmulatorContext> StartAsync(ITestOutputHelper testOutputHelper)
        {
            var configurationPath = await WriteConfigurationFile(testOutputHelper);

            await TestcontainersSettings.ExposeHostPortsAsync(5000);
            var container = BuildContainer(configurationPath);

            try
            {
                await container.StartAsync();
            }
            catch
            {
                try
                {
                    var logs = await container.GetLogsAsync();
                    testOutputHelper.WriteLine(logs.Stdout);
                    testOutputHelper.WriteLine(logs.Stderr);
                }
                catch
                {
                    // do not hide the original exception
                }

                throw;
            }

            return new EventGridEmulatorContext(container);
        }

        private static IContainer BuildContainer(string configurationPath)
        {
            return new ContainerBuilder()
                .WithImage("workleap/eventgridemulator:0.6.0")
                .WithPortBinding(EmulatorPort, assignRandomHostPort: true)
                .WithBindMount(configurationPath, "/app/appsettings.json", AccessMode.ReadOnly)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(EmulatorPort))
                .Build();
        }

        private static async Task<string> WriteConfigurationFile(ITestOutputHelper testOutputHelper)
        {
            var path = Path.GetTempFileName();
            await File.WriteAllTextAsync(path, GetConfiguration());
            if (!OperatingSystem.IsWindows())
            {
                await CliWrap.Cli.Wrap("chmod").WithArguments(["0444", path]).ExecuteAsync();
            }

            // For debug purposes only
            testOutputHelper.WriteLine("Write configuration file at: " + path);
            testOutputHelper.WriteLine("Write configuration content: " + await File.ReadAllTextAsync(path));

            return path;
        }

        private static string GetConfiguration()
        {
            return $$"""
                     {
                       "Topics": {
                         "{{TopicName}}": [
                           "{{LocalUrl}}{{SubscriberEndpoint}}"
                         ]
                       }
                     }
                     """;
        }

        public async ValueTask DisposeAsync()
        {
            await container.DisposeAsync();
        }
    }
}