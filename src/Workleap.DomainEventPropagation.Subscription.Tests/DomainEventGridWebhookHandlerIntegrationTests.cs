using Azure.Messaging.EventGrid;
using GSoft.Extensions.Xunit;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Officevibe.DomainEvents;
using Workleap.DomainEventPropagation.Subscription.Tests.OfficevibeMigration;

namespace Workleap.DomainEventPropagation.Subscription.Tests;

public sealed class DomainEventGridWebhookHandlerIntegrationTests :
    BaseIntegrationTest<DomainEventGridWebhookHandlerIntegrationTests.OfficevibeSubscriptionMigrationFixture>
{
    private static readonly OfficevibeEvent DomainEvent = new()
    {
        Number = 25,
        Text = "Hello world",
        MeasuringUnit = MeasuringUnit.Imperial,
        OfficevibeDate = DateTime.UtcNow,
    };

    private static readonly IDomainEventSubscriptionContext DomainEventSubscriptionContext = new DomainEventSubscriptionContext();

    public DomainEventGridWebhookHandlerIntegrationTests(OfficevibeSubscriptionMigrationFixture fixture, ITestOutputHelper testOutputHelper) : base(fixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task GivenDomainEvent_WhenHandleEventGridWebhookEventAsync_ThenEventHandled()
    {
        // Given
        var domainEventWrapper = DomainEventWrapper.Wrap(DomainEvent);

        var eventGridEvent = new EventGridEvent(
            domainEventWrapper.DomainEventName,
            domainEventWrapper.DomainEventName,
            "1.0",
            new BinaryData(domainEventWrapper.Data));

        var webhookHandler = this.Services.GetRequiredService<IDomainEventGridWebhookHandler>();

        // When
        await webhookHandler.HandleEventGridWebhookEventAsync(eventGridEvent, DomainEventSubscriptionContext, CancellationToken.None);
        var testState = this.Services.GetRequiredService<DomainEventGridWebhookHandlerTestState>();

        // Then
        Assert.Equal(1, testState.OfficevibeDomainEventHandlerCallCount);
        Assert.NotNull(testState.OfficevibeEvent);
        Assert.Equal(DomainEvent.Number, testState.OfficevibeEvent.Number);
        Assert.Equal(DomainEvent.MeasuringUnit, testState.OfficevibeEvent.MeasuringUnit);
        Assert.Equal(DomainEvent.OfficevibeDate, testState.OfficevibeEvent.OfficevibeDate);
    }

    [Fact]
    public async Task GivenEventSerializedFromOfficevibe_WhenHandleEventGridWebhookEventAsync_ThenEventHandled()
    {
        // Given

        // Serializing the same way the Officevibe library does
        var serializedEvent = JsonConvert.SerializeObject(DomainEvent);
        var eventGridEvent = new EventGridEvent("subject", DomainEvent.GetType().FullName, "1.0", BinaryData.FromString(serializedEvent));

        var webhookHandler = this.Services.GetRequiredService<IDomainEventGridWebhookHandler>();

        // When
        await webhookHandler.HandleEventGridWebhookEventAsync(eventGridEvent, DomainEventSubscriptionContext, CancellationToken.None);
        var testState = this.Services.GetRequiredService<DomainEventGridWebhookHandlerTestState>();

        // Then
        Assert.Equal(1, testState.OfficevibeDomainEventHandlerCallCount);
        Assert.NotNull(testState.OfficevibeEvent);
        Assert.Equal(DomainEvent.Number, testState.OfficevibeEvent.Number);
        Assert.Equal(DomainEvent.MeasuringUnit, testState.OfficevibeEvent.MeasuringUnit);
        Assert.Equal(DomainEvent.OfficevibeDate, testState.OfficevibeEvent.OfficevibeDate);
    }

    public sealed class OfficevibeSubscriptionMigrationFixture : BaseIntegrationFixture
    {
        public override IServiceCollection ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            services.AddEventPropagationSubscriber()
                .AddDomainEventHandler<OfficevibeEvent, OfficevibeDomainEventHandler>();

            services.AddSingleton<DomainEventGridWebhookHandlerTestState>();

            return services;
        }
    }

    public class DomainEventGridWebhookHandlerTestState
    {
        public int OfficevibeDomainEventHandlerCallCount { get; set; }

        public OfficevibeEvent? OfficevibeEvent { get; set; }
    }
}