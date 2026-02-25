namespace BlazorStore.Features.YouTubePlayer.State;

/// <summary>
/// Discriminated union representing the YouTube iframe player's lifecycle.
/// Mirrors the YouTube IFrame API player states as type-safe variants.
/// </summary>
public abstract record PlayerState
{
    private PlayerState() { }

    /// <summary>No video loaded yet.</summary>
    public sealed record Empty : PlayerState;

    /// <summary>A video is being loaded into the player.</summary>
    public sealed record Loading(string VideoId, bool Autoplay) : PlayerState;

    /// <summary>The player is buffering video data.</summary>
    public sealed record Buffering(string VideoId) : PlayerState;

    /// <summary>A video is currently playing.</summary>
    public sealed record Playing(string VideoId) : PlayerState;

    /// <summary>Playback is paused.</summary>
    public sealed record Paused(string VideoId) : PlayerState;

    /// <summary>The player encountered an error (e.g. video unavailable).</summary>
    public sealed record Error(string Message) : PlayerState;
}