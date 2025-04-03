# Workleap.DomainEventPropagation

[![build](https://img.shields.io/github/actions/workflow/status/workleap/workleap-domain-event-propagation/publish.yml?logo=github&branch=main)](https://github.com/workleap/workleap-domain-event-propagation/actions/workflows/publish.yml)

| Package                                                          | Download Link                                                                                                                                                                                                               | Description                                                                                 |
|------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------|
| Workleap.DomainEventPropagation.Abstractions                     | [![nuget](https://img.shields.io/nuget/v/Workleap.DomainEventPropagation.Abstractions.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Abstractions)                                         | Contains abstractions that are used for publishing and receiving events                     |
| Workleap.DomainEventPropagation.Publishing                       | [![nuget](https://img.shields.io/nuget/v/Workleap.DomainEventPropagation.Publishing.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Publishing)                                             | Contains types used to publish events from any kind of .NET application                     |
| Workleap.DomainEventPropagation.Publishing.ApplicationInsights   | [![nuget](https://img.shields.io/nuget/v/Workleap.DomainEventPropagation.Publishing.ApplicationInsights.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Publishing.ApplicationInsights)     | Adds Application Insights distributed tracing when publishing events                        |
| Workleap.DomainEventPropagation.Subscription                     | [![nuget](https://img.shields.io/nuget/v/Workleap.DomainEventPropagation.Subscription.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Subscription)                                         | Contains types for an ASP.NET Core app to subscribe to Event Grid topics and receive events using push delivery |
| Workleap.DomainEventPropagation.Subscription.PullDelivery        | [![nuget](https://img.shields.io/nuget/v/Workleap.DomainEventPropagation.Subscription.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Subscription.PullDelivery)                             | Contains types for an ASP.NET Core app to subscribe to Event Grid topics and receive events using pull delivery |
| Workleap.DomainEventPropagation.Subscription.ApplicationInsights | [![nuget](https://img.shields.io/nuget/v/Workleap.DomainEventPropagation.Subscription.ApplicationInsights.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Subscription.ApplicationInsights) | Adds Application Insights distributed tracing when receiving events                         |

These libraries must be used in conjunction with [Azure Event Grid](https://learn.microsoft.com/en-us/azure/event-grid/) in order to publish and receive domain events.
It is meant to be used in a multi-services architecture where each service is responsible for its own data and publishes events to notify other services of changes. Read more about this in the [architecture center](https://gsoftdev.atlassian.net/wiki/spaces/TEC/pages/3914039609/How+should+two+services+use+Event+Grid+to+communicate)

For the offering to work as intended, users need to use the Publishing lbirary and either of the Subscription libraries. It is **required** to publish domain events with our Publishing library and subscribe to domain events with either the `Subscription` or `Subscription.PullDelivery` libraries depending on which of the Push or Pull delivery methods is preferred.

## Getting started

### Limitations

For now, librairies has the following limitations :
- Receiving Cloud Events using push delivery
- Using Event Grid events with Namespace Topics (Microsoft limitation)
- Publishing either Cloud or Event Grid events to multiple topics
- Application Insights telemetry for Cloud Event publishing
- Tracing for Cloud Event publishing
- `IPublishingDomainEventBehavior` behaviors for pull delivery

### Publish domain events

*Note that in order to use pull-delivery with Event Grid, you will need to leverage namespace Topic. It's important to know that those topics only support CloudEvent v1.0 schema. More information on namespace topics can be [found here](https://learn.microsoft.com/en-us/azure/event-grid/publish-events-using-namespace-topics)*

Install the package [Workleap.DomainEventPropagation.Publishing](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Publishing) in your .NET project that wants to send events to an Event Grid topic.
Then, you can use one of the following methods to register the required services.

```csharp
// Method 1: Automatically bind the options to a well-known configuration section
services.AddEventPropagationPublisher();

// appsetting.json (or any other configuration source)
{
  "EventPropagation": {
    "Publisher": {
      "TopicEndpoint": "<azure_topic_uri>",
      "TopicAccessKey": "<secret_value>",
      "TopicType": "<custom/namespace>", // Custom if unset
      "TopicName": "<azure_namespacetopic_name>" // Mandatory only if topic type = namespace
    }
  }
}

// Method 2: Automatically bind the options to a well-known configuration section with Azure Identity (RBAC)
services.AddEventPropagationPublisher(options =>
{
    options.TokenCredential = new DefaultAzureCredential();
});

// appsetting.json (or any other configuration source)
{
  "EventPropagation": {
    "Publisher": {
      "TopicEndpoint": "<azure_topic_uri>",
      "TopicType": "<custom/namespace>", // Custom if unset
      "TopicName": "<azure_namespacetopic_name>" // Mandatory only if topic type = namespace
    }
  }
}

// Method 3: Set options values directly in C#
services.AddEventPropagationPublisher(options =>
{
    options.TopicEndpoint = "<azure_topic_uri>";

    // Using an access key        
    options.TopicAccessKey = "<secret_value>";
    
    // Using Azure Identity (RBAC)
    options.TokenCredential = new DefaultAzureCredential();

    // Topic type, Custom by default
    options.TopicType = TopicType.Custom;

    // Namespace topic name, mandatory if topic type = namespace
    options.TopicName = "<azure_namespacetopic_name>";
});
```

> Note that you can use either an access key or a token credential in order to authenticate to your eventGrid topic, not both.

Then, in order to publish a domain event, you first need to define your domain events using the `IDomainEvent` interface.
Decorate the domain event with the `[DomainEvent]` attribute, specifying a unique event name.

```csharp
[DomainEvent("example")]
public class ExampleDomainEvent : IDomainEvent
{
    public string Id { get; set; }
}

// Or if you want to specify what schema should be used
[DomainEvent("example", EventSchema.CloudEvent)]
public class ExampleDomainEvent : IDomainEvent
{
    public string Id { get; set; }
}
```

Once your domain events are defined, you can inject and use the `IEventPropagationClient` service.

```csharp
var domainEvent = new ExampleDomainEvent
{
    Id = Guid.NewGuid().ToString()
};

await this._eventPropagationClient.PublishDomainEventAsync(domainEvent, CancellationToken.None);
```

You can specify additional metadata for your domain events to leverage subscription filtering within Azure.
*This is only supported on domain events that use the CloudEvent schema.*

```csharp
var domainEvent = new ExampleDomainEvent
{
    Id = Guid.NewGuid().ToString()
};

await this._eventPropagationClient.PublishDomainEventAsync(domainEvent, x => x.Subject = "<custom_subject>", CancellationToken.None);
```


### Subscribe to domain events with push delivery

Install the package [Workleap.DomainEventPropagation.Subscription](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Subscription) in your ASP.NET Core project that wants to receive events from Event Grid topics.

You can define your domain event handlers by implementing the `IDomainEventHandler<>` interface and then registering them in the service collection later.

```csharp
public class ExampleDomainEventHandler : IDomainEventHandler<ExampleDomainEvent>
{
    public Task HandleDomainEventAsync(ExampleDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Do something with the domain event
        return Task.CompletedTask;
    }
}
```

Then, you can use on of the following methods to register the required services and map the webhook endpoint.

```csharp
// Method 1: Register only selected domain event handlers
services.AddEventPropagationSubscriber()
    .AddDomainEventHandler<ExampleDomainEvent, ExampleDomainEventHandler>()
    .AddDomainEventHandler<OtherDomainEvent, OtherDomainEventHandler>();

// Method 2: Register all domain event handlers from a given assembly
services.AddEventPropagationSubscriber()
    .AddDomainEventHandlers(Assembly.GetExecutingAssembly());

// Register the webhook endpoint in your ASP.NET Core app (startup-based approach)
app.UseEndpoints(builder =>
{
    builder.MapEventPropagationEndpoint();
});

// Register the webhook endpoint in your ASP.NET Core app (minimal APIs approach)
app.MapEventPropagationEndpoint();
```


#### Securing the webhook endpoint

It is required to expose an ASP.NET Core endpoint in order for Event Grid topics to be able to push events.
By default, the registered endpoint will allow anonymous access, but it is possible to secure it as shown below:

```csharp
// "RequireAuthorization" is a built-in ASP.NET Core method so you can specify any authorization policy you want
app.MapEventPropagationEndpoint().RequireAuthorization();
```

Now, follow this [Microsoft documentation](https://learn.microsoft.com/en-us/azure/event-grid/secure-webhook-delivery#deliver-events-to-a-webhook-in-the-same-azure-ad-tenant) to continue the configuration.  


### Subscribe to domain events with pull delivery

Install the package [Workleap.DomainEventPropagation.Subscription.PullDelivery](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Subscription.PullDelivery) in your ASP.NET Core project that wants to receive events from Event Grid topics.
First, you will need to use one of the following methods to register the required services.

```csharp
// Method 1: Register to pull delivery and bind the subscription options to the well-known configuration section named EventPropagation:Subscription
services.AddPullDeliverySubscription()
  .AddTopicSubscription();

// appsetting.json (or any other configuration source)
{
  "EventPropagation": {
    "Subscription": {
      "TopicEndpoint": "<azure_topic_uri>",
      "TopicName": "<namespace_topic_to_listen_to>"
      "SubscriptionName": "<subscription_name_under_specified_topic>",
      "MaxDegreeOfParallelism": 10,
      "MaxRetries": 3,
      "TopicAccessKey": "<secret_value>", // Can be omitted to use Azure Identity (RBAC)
    }
  }
}

// Method 2: Register to pull delivery and bind to multiple subscriptions
services.AddPullDeliverySubscription()
  .AddTopicSubscription("EventPropagation:TopicSub1")
  .AddTopicSubscription("EventPropagation:TopicSub2");

// appsetting.json (or any other configuration source)
{
  "EventPropagation": {
    "TopicSub1": {
      "TopicEndpoint": "<azure_topic_uri>",
      "TopicName": "<namespace_topic_to_listen_to>"
      "SubscriptionName": "<subscription_name_under_specified_topic>",
      "MaxDegreeOfParallelism": 10,
      "MaxRetries": 3,
      "TopicAccessKey": "<secret_value>", // Can be omitted to use Azure Identity (RBAC)
    },
    "TopicSub2": {
      "TopicEndpoint": "<azure_topic_uri>",
      "TopicName": "<namespace_topic_to_listen_to>"
      "SubscriptionName": "<subscription_name_under_specified_topic>",
      "MaxDegreeOfParallelism": 10,
      "MaxRetries": 10,
      "TopicAccessKey": "<secret_value>", // Can be omitted to use Azure Identity (RBAC)
    }
  }
}

// Method 3: Set options values directly in C#
services.AddPullDeliverySubscription()
  .AddTopicSubscription("EventPropagation:TopicSub1", options => 
  {
    options.TopicEndpoint = "<azure_topic_uri>";

    // Namespace topic name
    options.TopicName = "<topic_name>";

    // Namespace topic subscription name
    options.SubscriptionName = "<subscription_name>";

    // Maximum degree of parallelism for processing events
    options.MaxDegreeOfParallelism = 10;
    
    // Client side max number of retries before being sent to the Dead Letter Queue
    options.MaxRetries = 10;

    // Using an access key        
    options.TopicAccessKey = "<secret_value>";
    
    // Using Azure Identity (RBAC)
    options.TokenCredential = new DefaultAzureCredential();
  })
  .AddTopicSubscription("EventPropagation:TopicSub2", options => 
  {
    // ...
  });

```

Then you can define your domain event handlers by implementing the `IDomainEventHandler<>` interface and register them using

```csharp
// Method 1: Register only selected domain event handlers
services.AddPullDeliverySubscription()
    .AddDomainEventHandler<ExampleDomainEvent, ExampleDomainEventHandler>()
    .AddDomainEventHandler<OtherDomainEvent, OtherDomainEventHandler>();

// Method 2: Register all domain event handlers from a given assembly
services.AddPullDeliverySubscription()
    .AddDomainEventHandlers(Assembly.GetExecutingAssembly());

// Handler sample
public class ExampleDomainEventHandler : IDomainEventHandler<ExampleDomainEvent>
{
    public Task HandleDomainEventAsync(ExampleDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        // Do something with the domain event
        return Task.CompletedTask;
    }
}
```

#### Client Side Max Retries Count

To ensure that messages end up in the Dead Letter Queue, a client-side retry count is required. Otherwise, when a message is requeued (released), Event Grid ignores the subscription retry count, and if the event exceeds its time-to-live, it is silently dropped.

It can be configured by setting the `MaxRetries` property in the `EventGridSubscriptionClientOptions`. The default value is 3.

## Configure the underlying Event Grid clients options

You can use the [named options pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#named-options-support-using-iconfigurenamedoptions) to configure the behavior of the underlying Event Grid clients. For instance:

```csharp
// Configure options when using EventGridEvents
services.Configure<EventGridPublisherClientOptions>(EventPropagationPublisherOptions.EventGridClientName, options =>
{
    options.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);
});

// Configure options when using CloudEvents
services.Configure<EventGridSenderClientOptions>(EventPropagationPublisherOptions.EventGridClientName, options =>
{
    options.Retry.NetworkTimeout = TimeSpan.FromSeconds(15);
});

```

## Additional notes

* You may only define one domain event handler per domain event you wish to handle. If you would require more, use the single allowed domain event handler as a facade for multiple operations.
* Domain event handlers must have idempotent behavior (you could execute it multiple times for the same event and the result would always be the same).
* If your domain event types and handlers are in dedicated assemblies, you can reference the [Workleap.DomainEventPropagation.Abstractions](https://www.nuget.org/packages/Workleap.DomainEventPropagation.Abstractions) packages in order to avoid a dependency on third-parties like Azure and Microsoft extensions.
* For improved performance, it is possible to run multiple event handlers in parallel by adjusting the `MaxDegreeOfParallelism` option in the subscription configuration. The default value is 1.

## Building, releasing and versioning

The project can be built by running `Build.ps1`. It uses [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) to help detect public API breaking changes. Use the built-in roslyn analyzer to ensure that public APIs are declared in `PublicAPI.Shipped.txt`, and obsolete public APIs in `PublicAPI.Unshipped.txt`.

A new *preview* NuGet package is **automatically published** on any new commit on the main branch. This means that by completing a pull request, you automatically get a new NuGet package.

When you are ready to **officially release** a stable NuGet package by following the [SemVer guidelines](https://semver.org/), simply **manually create a tag** with the format `x.y.z`. This will automatically create and publish a NuGet package for this version.

## Included Roslyn analyzers

| Rule ID | Category | Severity | Description                                                        |
|---------|----------|----------|--------------------------------------------------------------------|
| WLDEP01 | Usage    | Warning  | Use DomainEvent attribute on event                                 |
| WLDEP02 | Usage    | Warning  | Use unique event name in attribute                                 |
| WLDEP03 | Usage    | Warning  | Ensure event name follows the naming convention                    |

To modify the severity of one of these diagnostic rules, you can use a `.editorconfig` file. For example:

```ini
## Disable analyzer for test files
[**Tests*/**.cs]
dotnet_diagnostic.WLDEP01.severity = none
dotnet_diagnostic.WLDEP02.severity = none
dotnet_diagnostic.WLDEP03.severity = none
```

To learn more about configuring or suppressing code analysis warnings, refer to [this documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/suppress-warnings).

## License

Copyright © 2023, Workleap This code is licensed under the Apache License, Version 2.0. You may obtain a copy of this license at https://github.com/workleap/gsoft-license/blob/master/LICENSE.

