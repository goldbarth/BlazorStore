namespace ArcFlow.Features.YouTubePlayer.State;

public abstract record PlaybackDecision
{
    private PlaybackDecision() { }

    public sealed record AdvanceTo(Guid ItemId) : PlaybackDecision;
    public sealed record Stop : PlaybackDecision;
    public sealed record NoOp : PlaybackDecision;
}
