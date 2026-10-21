using System.Diagnostics;

namespace WorkoutTracker.Tests;

// Spans started while it's open land in its trace, so a test can pick out its own spans even when other tests
// render the same page in parallel.
public sealed class TestTrace : IDisposable
{
    const string SourceName = "WorkoutTracker.Tests";
    static readonly ActivitySource Source = new(SourceName);

    readonly ActivityListener listener;
    readonly Activity root;

    public TestTrace()
    {
        listener = new ActivityListener
        {
            // By name: comparing with Source would read the field while its own constructor asks this listener.
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        root = Source.StartActivity("Test")!;
    }

    public ActivityTraceId Id => root.TraceId;

    public void Dispose()
    {
        root.Dispose();
        listener.Dispose();
    }
}
