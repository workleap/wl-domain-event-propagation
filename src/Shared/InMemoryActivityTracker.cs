using System.Diagnostics;

namespace Workleap.DomainEventPropagation.Tests;

internal sealed class InMemoryActivityTracker : IDisposable
{
    private static readonly HashSet<InMemoryActivityTracker> ActiveTrackers = new HashSet<InMemoryActivityTracker>();
    private static readonly object ActiveTrackersLock = new object();

    private readonly List<Activity> _activities;
    private readonly object _activitiesLock = new object();

    static InMemoryActivityTracker()
    {
        // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-collection-walkthroughs?source=recommendations#add-code-to-collect-the-traces
        var staticListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.Contains("Workleap.DomainEventPropagation"),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (ActiveTrackersLock)
                {
                    foreach (var tracker in ActiveTrackers)
                    {
                        tracker.TrackActivity(activity);
                    }
                }
            },
        };

        ActivitySource.AddActivityListener(staticListener);
    }

    public InMemoryActivityTracker()
    {
        this._activities = new List<Activity>();

        lock (ActiveTrackersLock)
        {
            ActiveTrackers.Add(this);
        }
    }

    private void TrackActivity(Activity activity)
    {
        lock (this._activitiesLock)
        {
            this._activities.Add(activity);
        }
    }

    public void AssertPublishSuccessful(string activityName)
    {
        lock (this._activitiesLock)
        {
            var activity = Assert.Single(this._activities, activity => activity.OperationName == activityName);

            Assert.Equal(activityName, activity.OperationName);
            Assert.Equal(ActivityKind.Producer, activity.Kind);
            Assert.Equal(ActivityStatusCode.Ok, activity.Status);
            Assert.Equal("OK", activity.GetTagItem(TracingHelper.StatusCodeTag));
        }
    }

    public void AssertPublishFailed(string activityName, Exception exception)
    {
        lock (this._activitiesLock)
        {
            var activity = Assert.Single(this._activities);

            Assert.Equal(activityName, activity.OperationName);
            Assert.Equal(activityName, activity.DisplayName);
            Assert.Equal(ActivityKind.Producer, activity.Kind);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);

            Assert.Equal("ERROR", activity.GetTagItem(TracingHelper.StatusCodeTag));
            Assert.Equal(exception.Message, activity.GetTagItem(TracingHelper.StatusDescriptionTag));
            Assert.Equal(exception.Message, activity.GetTagItem(TracingHelper.ExceptionMessageTag));
            Assert.Equal(exception.GetType().FullName!, activity.GetTagItem(TracingHelper.ExceptionTypeTag));

            var stacktrace = Assert.IsType<string>(activity.GetTagItem(TracingHelper.ExceptionStackTraceTag));
            Assert.NotEmpty(stacktrace);
        }
    }

    public void AssertSubscribeSuccessful(string activityName)
    {
        lock (this._activitiesLock)
        {
            Assert.Contains(this._activities, activity => activity.OperationName == activityName);
            Assert.All(
                this._activities.Where(activity => activity.OperationName == activityName),
                activity =>
                {
                    Assert.Equal(activityName, activity.OperationName);
                    Assert.Equal(ActivityKind.Consumer, activity.Kind);
                    Assert.Equal(ActivityStatusCode.Ok, activity.Status);
                    Assert.Equal("OK", activity.GetTagItem(TracingHelper.StatusCodeTag));
                    Assert.Single(activity.Links);
                });
        }
    }

    public void AssertSubscriptionFailed(string activityName, Exception exception)
    {
        lock (this._activitiesLock)
        {
            var activity = Assert.Single(this._activities);
            Assert.Equal(activityName, activity.OperationName);
            Assert.Equal(activityName, activity.DisplayName);
            Assert.Equal(ActivityKind.Consumer, activity.Kind);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);

            Assert.Equal("ERROR", activity.GetTagItem(TracingHelper.StatusCodeTag));
            Assert.Equal(exception.Message, activity.GetTagItem(TracingHelper.StatusDescriptionTag));
            Assert.Equal(exception.Message, activity.GetTagItem(TracingHelper.ExceptionMessageTag));
            Assert.Equal(exception.GetType().FullName!, activity.GetTagItem(TracingHelper.ExceptionTypeTag));

            var stacktrace = Assert.IsType<string>(activity.GetTagItem(TracingHelper.ExceptionStackTraceTag));
            Assert.NotEmpty(stacktrace);
        }
    }

    public void AssertCloudEventsTagsSet(string activityName)
    {
        lock (this._activitiesLock)
        {
            Assert.Contains(this._activities, activity => activity.OperationName == activityName);
            Assert.All(
                this._activities.Where(activity => activity.OperationName == activityName),
                activity =>
                {
                    Assert.NotNull(activity.GetTagItem(TracingHelper.CloudEventsIdTag));
                    Assert.NotNull(activity.GetTagItem(TracingHelper.CloudEventsSourceTag));
                    Assert.NotNull(activity.GetTagItem(TracingHelper.CloudEventsTypeTag));
                });
        }
    }

    public void AssertEventgridEventsTagsSet(string activityName)
    {
        lock (this._activitiesLock)
        {
            Assert.Contains(this._activities, activity => activity.OperationName == activityName);
            Assert.All(
                this._activities.Where(activity => activity.OperationName == activityName),
                activity =>
                {
                    Assert.NotNull(activity.GetTagItem(TracingHelper.EventgridEventsIdTag));
                    Assert.NotNull(activity.GetTagItem(TracingHelper.EventgridEventsSourceTag));
                    Assert.NotNull(activity.GetTagItem(TracingHelper.EventgridEventsTypeTag));
                });
        }
    }

    public void AssertNoSubscribeActivity()
    {
        lock (this._activitiesLock)
        {
            Assert.DoesNotContain(this._activities, activity => activity.OperationName == "EventGridEvents process");
        }
    }

    public void Dispose()
    {
        lock (ActiveTrackersLock)
        {
            ActiveTrackers.Remove(this);
        }
    }
}