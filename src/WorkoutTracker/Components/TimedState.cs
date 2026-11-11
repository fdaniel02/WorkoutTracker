namespace WorkoutTracker.Components;

// The timer is stored as timestamps, so the elapsed time stays correct after a reload,
// including the time the phone was locked.
public abstract class TimedState
{
    public TimeSpan AccumulatedBeforePause { get; set; }
    public DateTime? RunningSinceUtc { get; set; }

    public bool Running => RunningSinceUtc is not null;

    public TimeSpan Elapsed(DateTime nowUtc) =>
        AccumulatedBeforePause + (RunningSinceUtc is { } since ? nowUtc - since : TimeSpan.Zero);

    public virtual void ToggleTimer(DateTime nowUtc)
    {
        if (Running)
            Stop(nowUtc);
        else
            RunningSinceUtc = nowUtc;
    }

    public void Stop(DateTime nowUtc)
    {
        AccumulatedBeforePause = Elapsed(nowUtc);
        RunningSinceUtc = null;
    }

    protected void RestartTimer(DateTime nowUtc)
    {
        AccumulatedBeforePause = TimeSpan.Zero;
        RunningSinceUtc = nowUtc;
    }
}
