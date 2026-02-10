using System.Collections.Immutable;
using ArcFlow.Features.YouTubePlayer.Models;

namespace ArcFlow.Features.YouTubePlayer.State;

public record YtAction
{
    // App/Feature lifecycle
    public sealed record Initialize : YtAction;
    // Data loaded (results)
    public sealed record PlaylistsLoaded(ImmutableList<Playlist> Playlists) : YtAction;
    public sealed record PlaylistLoaded(Playlist Playlist) : YtAction;
    
    // User intent (commands)
    public sealed record CreatePlaylist(string Name, string? Description) : YtAction;
    public sealed record AddVideo(Guid PlaylistId, string Url, string Title) : YtAction;
    
    public sealed record SelectPlaylist(Guid PlaylistId) : YtAction;
    public sealed record SelectVideo(int Index, bool Autoplay) : YtAction;

    public sealed record SortChanged(int OldIndex, int NewIndex) : YtAction;
    
    // Interop events
    public sealed record PlayerStateChanged(int YtState, string VideoId) : YtAction;
    public sealed record VideoEnded : YtAction;
    
    // Error surface
    public sealed record OperationFailed(string Message) : YtAction;
}