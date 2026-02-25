namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Discriminated union representing the outcome of a playback navigation computation.
/// Returned by <see cref="PlaybackNavigation"/> to tell the store what to do next.
/// </summary>
public abstract record PlaybackDecision
{
    private PlaybackDecision() { }

    /// <summary>Advance playback to the video identified by <paramref name="ItemId"/>.</summary>
    public sealed record AdvanceTo(Guid ItemId) : PlaybackDecision;

    /// <summary>Stop playback (end of queue or empty queue).</summary>
    public sealed record Stop : PlaybackDecision;

    /// <summary>No action required (e.g. no previous track available).</summary>
    public sealed record NoOp : PlaybackDecision;
}
