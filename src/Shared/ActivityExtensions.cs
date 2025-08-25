using System.Diagnostics;

namespace Workleap.DomainEventPropagation;

internal static class ActivityExtensions
{
    public static void ExecuteAsCurrentActivity<TState>(this Activity newCurrentActivity, TState state, Action<TState> action)
    {
        var oldCurrentActivity = Activity.Current;
        Activity.Current = newCurrentActivity;

        try
        {
            action(state);
        }
        finally
        {
            Activity.Current = oldCurrentActivity;
        }
    }
}