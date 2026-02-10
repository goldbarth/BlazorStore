namespace ArcFlow.Features.YouTubePlayer.State;

public abstract record PlayerState
{
    private PlayerState() { } // Prevents external inheritance
    
    public sealed record Empty : PlayerState;
    public sealed record Loading(string VideoId, bool Autoplay) : PlayerState;
    public sealed record Buffering(string VideoId) : PlayerState;
    public sealed record Playing(string VideoId) : PlayerState;
    public sealed record Paused(string VideoId) : PlayerState;
    public sealed record Error(string Message) : PlayerState;
}